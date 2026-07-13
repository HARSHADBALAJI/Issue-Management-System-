using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using TicketSystem.Api.Data;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class EmailOutboxProcessor : BackgroundService, IEmailOutboxService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailOutboxProcessor> _logger;

    private static readonly int[] RetryDelaysMinutes = [1, 5, 15, 60, 360, 1440];

    public EmailOutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EnqueueAsync(string recipientEmail, string subject, string bodyHtml, string? bodyPlainText,
        int? ticketId = null, int? requesterId = null, int? userId = null,
        string? inReplyTo = null, string? references = null,
        string? senderEmail = null, string? recipientName = null,
        int? ticketMessageId = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TicketSystemDbContext>();

        var email = new EmailOutbox
        {
            RecipientEmail = recipientEmail,
            RecipientName = recipientName,
            SenderEmail = senderEmail ?? "noreply@ticketingsystem.com",
            Subject = subject,
            BodyHtml = bodyHtml,
            BodyPlainText = bodyPlainText,
            InReplyTo = inReplyTo,
            References = references,
            TicketId = ticketId,
            RequesterId = requesterId,
            UserId = userId,
            TicketMessageId = ticketMessageId,
            Status = "Pending",
            NextAttemptAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        ctx.EmailOutbox.Add(email);
        await ctx.SaveChangesAsync();

        _logger.LogDebug("Enqueued email to {Recipient} subject={Subject} id={Id}", recipientEmail, subject, email.Id);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailOutboxProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailOutboxProcessor cycle failed");
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TicketSystemDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var batch = await ctx.EmailOutbox
            .Where(e => e.Status == "Pending" || e.Status == "Failed" && e.NextAttemptAt <= DateTime.UtcNow)
            .OrderBy(e => e.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? config["Email:Smtp:Host"] ?? "";
        var smtpPort = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? config["Email:Smtp:Port"], out var p) ? p : 587;
        var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ?? config["Email:Smtp:User"] ?? "";
        var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? config["Email:Smtp:Password"] ?? "";
        var systemEmail = Environment.GetEnvironmentVariable("SYSTEM_USER_EMAIL") ?? config["Email:SystemEmail"] ?? "noreply@ticketingsystem.com";

        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogWarning("SMTP not configured. Skipping {Count} queued emails", batch.Count);
            return;
        }

        foreach (var email in batch)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                email.Status = "Sending";
                email.LastAttemptAt = DateTime.UtcNow;
                await ctx.SaveChangesAsync(ct);

                List<TicketAttachment>? attachments = null;
                if (email.TicketMessageId.HasValue)
                {
                    attachments = await ctx.TicketAttachments
                        .Where(a => a.TicketMessageId == email.TicketMessageId.Value)
                        .ToListAsync(ct);
                }

                var message = BuildMessage(email, systemEmail, attachments);
                using var client = new SmtpClient();

                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls, ct);
                await client.AuthenticateAsync(smtpUser, smtpPassword, ct);
                await client.SendAsync(message, ct);
                await client.DisconnectAsync(true, ct);

                email.Status = "Sent";
                email.SentAt = DateTime.UtcNow;
                email.SentMessageId = message.MessageId;
                email.LastError = null;
                email.NextAttemptAt = null;

                _logger.LogInformation("Email sent to {To} subject={Subject} id={Id}",
                    email.RecipientEmail, email.Subject, email.Id);
            }
            catch (Exception ex)
            {
                email.RetryCount++;
                email.LastError = $"{ex.GetType().Name}: {ex.Message}";
                email.Status = email.RetryCount >= email.MaxRetries ? "DeadLetter" : "Failed";

                var delayIndex = Math.Min(email.RetryCount - 1, RetryDelaysMinutes.Length - 1);
                email.NextAttemptAt = DateTime.UtcNow.AddMinutes(RetryDelaysMinutes[delayIndex]);

                _logger.LogWarning(ex, "Email {Id} to {To} failed (attempt {Retry}/{Max}) status={Status} retry={Delay}m",
                    email.Id, email.RecipientEmail, email.RetryCount, email.MaxRetries, email.Status, RetryDelaysMinutes[delayIndex]);
            }

            await ctx.SaveChangesAsync(ct);
        }
    }

    private MimeMessage BuildMessage(EmailOutbox email, string systemEmail, List<TicketAttachment>? attachments = null)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Issue Management System", systemEmail));
        message.To.Add(new MailboxAddress(email.RecipientName ?? "", email.RecipientEmail));
        message.Subject = email.Subject;

        var bodyBuilder = new BodyBuilder();

        if (!string.IsNullOrWhiteSpace(email.BodyHtml) &&
            (email.BodyHtml.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase) ||
             email.BodyHtml.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase)))
        {
            bodyBuilder.HtmlBody = email.BodyHtml;
            var plainText = email.BodyPlainText ?? Regex.Replace(email.BodyHtml, @"<[^>]+>", " ");
            plainText = Regex.Replace(plainText, @"\s+", " ");
            bodyBuilder.TextBody = plainText.Trim();
        }
        else
        {
            bodyBuilder.TextBody = email.BodyHtml;
        }

        if (attachments?.Count > 0)
        {
            foreach (var attachment in attachments)
            {
                bodyBuilder.Attachments.Add(attachment.FileName, attachment.FileData, ContentType.Parse(attachment.ContentType));
            }
        }

        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            if (!string.IsNullOrWhiteSpace(email.InReplyTo))
            {
                var sanitized = SanitizeMsgId(email.InReplyTo);
                if (sanitized != null) message.InReplyTo = sanitized;
            }

            if (!string.IsNullOrWhiteSpace(email.References))
            {
                foreach (var refId in email.References.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var sanitized = SanitizeMsgId(refId);
                    if (sanitized != null) message.References.Add(sanitized);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set threading headers for outbox email {Id}", email.Id);
        }

        var emailParts = systemEmail.Split('@');
        var domain = emailParts.Length > 1 ? emailParts[^1] : "ticketingsystem.com";
        message.MessageId = $"<{Guid.NewGuid():N}@{domain}>";

        return message;
    }

    private static string? SanitizeMsgId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var trimmed = id.Trim();
        if (!trimmed.StartsWith('<')) trimmed = $"<{trimmed}";
        if (!trimmed.EndsWith('>')) trimmed = $"{trimmed}>";
        return trimmed;
    }
}
