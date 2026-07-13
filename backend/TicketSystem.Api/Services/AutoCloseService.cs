using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class AutoCloseService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoCloseService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _autoCloseAfter = TimeSpan.FromDays(3);

    public AutoCloseService(IServiceScopeFactory scopeFactory, ILogger<AutoCloseService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoCloseService started. Checking every {Interval} for tickets to auto-close.", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAutoCloseAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoCloseService cycle failed");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("AutoCloseService stopped");
    }

    private async Task ProcessAutoCloseAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TicketSystemDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var cutoff = DateTime.UtcNow.Add(-_autoCloseAfter);

        // Find tickets resolved > 3 days ago that haven't been closed and have no new messages
        var ticketsToClose = await context.Tickets
            .Include(t => t.Requester)
            .Include(t => t.Status)
            .Where(t => t.StatusId == 3 && t.ResolvedAt != null && t.ResolvedAt < cutoff)
            .ToListAsync(ct);

        foreach (var ticket in ticketsToClose)
        {
            // Check if there's been any new message since resolution (requester replied)
            var hasNewReply = await context.TicketMessages
                .AnyAsync(m => m.TicketId == ticket.Id && m.CreatedAt > ticket.ResolvedAt, ct);

            if (hasNewReply)
            {
                // Requester replied - cancel auto-close, set back to in_progress (or keep resolved)
                _logger.LogInformation("Ticket {TicketNumber} has new replies since resolve, skipping auto-close", ticket.TicketNumber);
                continue;
            }

            // Auto-close the ticket
            var fromStatusId = ticket.StatusId;
            ticket.StatusId = 4; // Closed
            ticket.ClosedAt = DateTime.UtcNow;
            ticket.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(ct);

            // Add status history
            var history = new TicketStatusHistory
            {
                TicketId = ticket.Id,
                FromStatusId = fromStatusId,
                ToStatusId = 4,
                Remarks = "Auto-closed after 3 days of resolution",
                CreatedAt = DateTime.UtcNow
            };
            context.TicketStatusHistories.Add(history);
            await context.SaveChangesAsync(ct);

            // Notify requester
            try
            {
                await emailService.SendAutoCloseEmailAsync(ticket, ticket.Requester);

                await notifService.CreateAsync(
                    0, // system notification - no specific user
                    ticket.Id,
                    "auto_closed",
                    $"Ticket {ticket.TicketNumber} auto-closed",
                    $"Your ticket has been closed due to no response within 3 days."
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send auto-close notification for ticket {TicketNumber}", ticket.TicketNumber);
            }

            await auditService.LogAsync("Ticket Auto-Closed", "Ticket", ticket.Id, newValues: $"Auto-closed after 3-day timer");

            _logger.LogInformation("Auto-closed ticket {TicketNumber} (Id={Id})", ticket.TicketNumber, ticket.Id);
        }
    }
}
