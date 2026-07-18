using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class EmailOutboxProcessor : BackgroundService, IEmailOutboxService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailOutboxProcessor> _logger;
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private static readonly int[] RetryDelaysMinutes = [1, 5, 15, 60, 360, 1440];

    public EmailOutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _http = new HttpClient();
    }

    public async Task EnqueueAsync(string recipientEmail, string subject, string bodyHtml, string? bodyPlainText,
        int? ticketId = null, int? requesterId = null, int? userId = null,
        string? inReplyTo = null, string? references = null,
        string? senderEmail = null, string? recipientName = null,
        int? ticketMessageId = null,
        List<InlineAttachmentInfo>? inlineAttachments = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TicketSystemDbContext>();

        string? inlineJson = null;
        if (inlineAttachments != null && inlineAttachments.Count > 0)
        {
            var items = inlineAttachments.Select(a => new
            {
                fileName = a.FileName,
                contentType = a.ContentType,
                contentId = a.ContentId,
                data = Convert.ToBase64String(a.Data)
            });
            inlineJson = System.Text.Json.JsonSerializer.Serialize(items);
        }

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
            InlineAttachmentsJson = inlineJson,
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

        _logger.LogDebug("Enqueued email to {Recipient} subject={Subject} id={Id} inlineAttachments={Count}", recipientEmail, subject, email.Id, inlineAttachments?.Count ?? 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailOutboxProcessor started (Gmail REST API mode)");

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

    private async Task EnsureAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-1))
            return;

        var clientId = Environment.GetEnvironmentVariable("GMAIL_CLIENT_ID") ?? "";
        var clientSecret = Environment.GetEnvironmentVariable("GMAIL_CLIENT_SECRET") ?? "";
        var refreshToken = Environment.GetEnvironmentVariable("GMAIL_REFRESH_TOKEN") ?? "";

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("Gmail OAuth2 credentials not configured. Cannot send emails.");
            return;
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        };

        var resp = await _http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form), ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _accessToken = root.GetProperty("access_token").GetString()!;
        var expiresIn = root.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

        _logger.LogDebug("Access token refreshed for email sending, expires in {Seconds}s", expiresIn);
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TicketSystemDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var batch = await ctx.EmailOutbox
            .Where(e => e.Status == "Pending" || (e.Status == "Failed" && e.NextAttemptAt <= DateTime.UtcNow))
            .OrderBy(e => e.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        var systemEmail = Environment.GetEnvironmentVariable("SYSTEM_USER_EMAIL")
            ?? Environment.GetEnvironmentVariable("SMTP_USER")
            ?? config["Email:SystemEmail"]
            ?? "digital.ti.lntecc@gmail.com";

        var clientId = Environment.GetEnvironmentVariable("GMAIL_CLIENT_ID") ?? "";
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("Gmail not configured. Skipping {Count} queued emails", batch.Count);
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

                await EnsureAccessTokenAsync(ct);

                if (string.IsNullOrWhiteSpace(_accessToken))
                {
                    throw new InvalidOperationException("Could not obtain Gmail access token");
                }

                var rawMime = BuildRawMime(email, systemEmail);
                await SendViaGmailApi(rawMime, ct);

                email.Status = "Sent";
                email.SentAt = DateTime.UtcNow;
                email.SentMessageId = Guid.NewGuid().ToString("N");
                email.LastError = null;
                email.NextAttemptAt = null;

                _logger.LogInformation("Email sent via Gmail API to {To} subject={Subject} id={Id}",
                    email.RecipientEmail, email.Subject, email.Id);
            }
            catch (Exception ex)
            {
                email.RetryCount++;
                email.LastError = $"{ex.GetType().Name}: {ex.Message}";
                email.Status = email.RetryCount >= email.MaxRetries ? "DeadLetter" : "Failed";

                var delayIndex = Math.Min(email.RetryCount - 1, RetryDelaysMinutes.Length - 1);
                email.NextAttemptAt = DateTime.UtcNow.AddMinutes(RetryDelaysMinutes[delayIndex]);

                _accessToken = null;

                _logger.LogWarning(ex, "Email {Id} to {To} failed (attempt {Retry}/{Max}) status={Status} retry={Delay}m",
                    email.Id, email.RecipientEmail, email.RetryCount, email.MaxRetries, email.Status, RetryDelaysMinutes[delayIndex]);
            }

            await ctx.SaveChangesAsync(ct);
        }
    }

    private async Task SendViaGmailApi(string rawMimeBase64, CancellationToken ct)
    {
        var userEmail = Environment.GetEnvironmentVariable("GMAIL_USER_EMAIL")
            ?? Environment.GetEnvironmentVariable("SMTP_USER")
            ?? "digital.ti.lntecc@gmail.com";

        var url = $"https://gmail.googleapis.com/gmail/v1/users/{userEmail}/messages/send";

        var body = JsonSerializer.Serialize(new { raw = rawMimeBase64 });
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _accessToken) },
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    private string BuildRawMime(EmailOutbox email, string systemEmail)
    {
        var sb = new StringBuilder();

        var boundary = $"boundary_{Guid.NewGuid():N}";
        var relatedBoundary = $"related_{Guid.NewGuid():N}";
        var messageId = $"<{Guid.NewGuid():N}@gmail.com>";

        sb.AppendLine($"From: Issue Management System <{systemEmail}>");
        sb.AppendLine($"To: <{email.RecipientEmail}>");
        if (!string.IsNullOrWhiteSpace(email.RecipientName))
        {
            sb.AppendLine($"To: =?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(email.RecipientName))}?= <{email.RecipientEmail}>");
        }
        else
        {
            sb.AppendLine($"To: <{email.RecipientEmail}>");
        }
        sb.AppendLine($"Subject: =?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(email.Subject))}?=");
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine($"Message-ID: {messageId}");
        sb.AppendLine($"Date: {DateTime.UtcNow:R}");

        if (!string.IsNullOrWhiteSpace(email.InReplyTo))
        {
            var sanitized = SanitizeMsgId(email.InReplyTo);
            if (sanitized != null) sb.AppendLine($"In-Reply-To: {sanitized}");
        }

        if (!string.IsNullOrWhiteSpace(email.References))
        {
            var refs = string.Join(" ",
                email.References.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(SanitizeMsgId)
                    .Where(r => r != null));
            if (!string.IsNullOrWhiteSpace(refs))
                sb.AppendLine($"References: {refs}");
        }

        var hasHtml = !string.IsNullOrWhiteSpace(email.BodyHtml) &&
            (email.BodyHtml.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase) ||
             email.BodyHtml.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase));

        var inlineAttachments = ParseInlineAttachments(email.InlineAttachmentsJson);

        if (hasHtml && inlineAttachments.Count > 0)
        {
            sb.AppendLine($"Content-Type: multipart/related; boundary=\"{relatedBoundary}\"");
            sb.AppendLine();

            sb.AppendLine($"--{relatedBoundary}");
            sb.AppendLine($"Content-Type: multipart/alternative; boundary=\"{boundary}\"");
            sb.AppendLine();

            sb.AppendLine($"--{boundary}");
            sb.AppendLine("Content-Type: text/plain; charset=\"UTF-8\"");
            sb.AppendLine("Content-Transfer-Encoding: quoted-printable");
            sb.AppendLine();
            var plainText = email.BodyPlainText ?? Regex.Replace(email.BodyHtml!, @"<[^>]+>", " ");
            plainText = Regex.Replace(plainText, @"\s+", " ").Trim();
            sb.AppendLine(plainText);

            sb.AppendLine($"--{boundary}");
            sb.AppendLine("Content-Type: text/html; charset=\"UTF-8\"");
            sb.AppendLine("Content-Transfer-Encoding: quoted-printable");
            sb.AppendLine();
            sb.AppendLine(email.BodyHtml);
            sb.AppendLine($"--{boundary}--");
            sb.AppendLine();

            foreach (var att in inlineAttachments)
            {
                sb.AppendLine($"--{relatedBoundary}");
                sb.AppendLine($"Content-Type: {att.ContentType}; name=\"{att.FileName}\"");
                sb.AppendLine($"Content-Disposition: inline; filename=\"{att.FileName}\"");
                sb.AppendLine($"Content-ID: <{att.ContentId}>");
                sb.AppendLine("Content-Transfer-Encoding: base64");
                sb.AppendLine();
                var base64 = Convert.ToBase64String(att.Data);
                for (var i = 0; i < base64.Length; i += 76)
                    sb.AppendLine(base64.Substring(i, Math.Min(76, base64.Length - i)));
            }

            sb.AppendLine($"--{relatedBoundary}--");
        }
        else if (hasHtml)
        {
            sb.AppendLine($"Content-Type: multipart/alternative; boundary=\"{boundary}\"");
            sb.AppendLine();

            sb.AppendLine($"--{boundary}");
            sb.AppendLine("Content-Type: text/plain; charset=\"UTF-8\"");
            sb.AppendLine("Content-Transfer-Encoding: quoted-printable");
            sb.AppendLine();
            var plainText = email.BodyPlainText ?? Regex.Replace(email.BodyHtml!, @"<[^>]+>", " ");
            plainText = Regex.Replace(plainText, @"\s+", " ").Trim();
            sb.AppendLine(plainText);

            sb.AppendLine($"--{boundary}");
            sb.AppendLine("Content-Type: text/html; charset=\"UTF-8\"");
            sb.AppendLine("Content-Transfer-Encoding: quoted-printable");
            sb.AppendLine();
            sb.AppendLine(email.BodyHtml);

            sb.AppendLine($"--{boundary}--");
        }
        else
        {
            sb.AppendLine("Content-Type: text/plain; charset=\"UTF-8\"");
            sb.AppendLine("Content-Transfer-Encoding: quoted-printable");
            sb.AppendLine();
            sb.AppendLine(email.BodyHtml ?? email.BodyPlainText ?? "");
        }

        var rawMime = sb.ToString();
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(rawMime))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static List<InlineAttachmentInfo> ParseInlineAttachments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray().Select(e => new InlineAttachmentInfo
            {
                FileName = e.GetProperty("fileName").GetString() ?? "attachment",
                ContentType = e.GetProperty("contentType").GetString() ?? "application/octet-stream",
                ContentId = e.GetProperty("contentId").GetString() ?? $"{Guid.NewGuid():N}",
                Data = Convert.FromBase64String(e.GetProperty("data").GetString() ?? "")
            }).ToList();
        }
        catch
        {
            return [];
        }
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
