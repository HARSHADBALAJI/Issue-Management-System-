using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;

namespace TicketSystem.Api.Services;

public class TrackingService : ITrackingService
{
    private readonly TicketSystemDbContext _context;
    private readonly ILogger<TrackingService> _logger;

    public TrackingService(TicketSystemDbContext context, ILogger<TrackingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GenerateTokenAsync(int ticketId, string email, int expiryDays = 30)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var tokenHash = ComputeHash(token);

        var entity = new Models.Entities.TicketAccessToken
        {
            TicketId = ticketId,
            TokenHash = tokenHash,
            Email = email.ToLowerInvariant().Trim(),
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            CreatedAt = DateTime.UtcNow
        };

        _context.TicketAccessTokens.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Generated tracking token for ticket {TicketId} email={Email}", ticketId, email);
        return token;
    }

    public async Task<TrackingValidationResult?> ValidateTokenAsync(int ticketId, string token)
    {
        var tokenHash = ComputeHash(token);

        var accessToken = await _context.TicketAccessTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.TicketId == ticketId);

        if (accessToken == null)
            return new TrackingValidationResult { IsValid = false, ErrorMessage = "Invalid tracking link." };

        if (accessToken.IsRevoked)
            return new TrackingValidationResult { IsValid = false, ErrorMessage = "This tracking link has been revoked." };

        if (accessToken.ExpiresAt < DateTime.UtcNow)
            return new TrackingValidationResult { IsValid = false, ErrorMessage = "This tracking link has expired." };

        accessToken.LastUsedAt = DateTime.UtcNow;
        accessToken.UsageCount++;
        await _context.SaveChangesAsync();

        var ticket = await _context.Tickets
            .Include(t => t.Requester)
            .Include(t => t.Application)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Status)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            return new TrackingValidationResult { IsValid = false, ErrorMessage = "Ticket not found." };

        var messages = await _context.TicketMessages
            .Include(m => m.User)
            .Include(m => m.Requester)
            .Include(m => m.Attachments)
            .Where(m => m.TicketId == ticketId && !m.IsInternal)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TrackingMessage
            {
                Id = m.Id,
                SenderName = m.User != null ? m.User.FullName : (m.Requester != null ? m.Requester.FullName : "Unknown"),
                SenderType = m.MessageSourceType ?? "Unknown",
                Content = m.Content ?? "",
                IsInternal = m.IsInternal,
                CreatedAt = m.CreatedAt,
                Attachments = m.Attachments.Select(a => new TrackingAttachment
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize
                }).ToList()
            })
            .ToListAsync();

        var statusHistory = await _context.TicketStatusHistories
            .Include(h => h.FromStatus)
            .Include(h => h.ToStatus)
            .Include(h => h.ChangedByUser)
            .Where(h => h.TicketId == ticketId)
            .OrderBy(h => h.CreatedAt)
            .Select(h => new TrackingStatusHistory
            {
                FromStatus = h.FromStatus != null ? h.FromStatus.DisplayName : "",
                ToStatus = h.ToStatus.DisplayName,
                ChangedBy = h.ChangedByUser != null ? h.ChangedByUser.FullName : null,
                Remarks = h.Remarks,
                CreatedAt = h.CreatedAt
            })
            .ToListAsync();

        return new TrackingValidationResult
        {
            IsValid = true,
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber ?? "",
            Subject = ticket.Subject ?? "",
            Description = ticket.Description ?? "",
            StatusName = ticket.Status?.DisplayName ?? "",
            StatusColor = ticket.Status?.Color ?? "",
            Priority = ticket.Priority ?? "",
            ApplicationName = ticket.Application?.Name ?? "",
            RequesterName = ticket.Requester?.FullName ?? "",
            RequesterEmail = ticket.Requester?.Email ?? "",
            AssignedToName = ticket.AssignedToUser?.FullName,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt ?? ticket.CreatedAt,
            ResolvedAt = ticket.ResolvedAt,
            ClosedAt = ticket.ClosedAt,
            Messages = messages,
            StatusHistory = statusHistory
        };
    }

    public async Task<List<TrackingTicketSummary>> GetMyTicketsAsync(string email, int? currentTicketId = null)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        var tickets = await _context.Tickets
            .Include(t => t.Requester)
            .Include(t => t.Application)
            .Include(t => t.Status)
            .Where(t => t.Requester != null && t.Requester.Email == normalizedEmail)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TrackingTicketSummary
            {
                TicketId = t.Id,
                TicketNumber = t.TicketNumber ?? "",
                Subject = t.Subject ?? "",
                StatusName = t.Status.DisplayName,
                StatusColor = t.Status.Color,
                Priority = t.Priority ?? "",
                ApplicationName = t.Application.Name,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt ?? t.CreatedAt
            })
            .ToListAsync();

        return tickets;
    }

    public async Task RevokeTokensForTicketAsync(int ticketId, string? email = null)
    {
        var query = _context.TicketAccessTokens.Where(t => t.TicketId == ticketId);
        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(t => t.Email == email.ToLowerInvariant().Trim());

        var tokens = await query.ToListAsync();
        foreach (var t in tokens)
            t.IsRevoked = true;

        await _context.SaveChangesAsync();
    }

    private static string ComputeHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
