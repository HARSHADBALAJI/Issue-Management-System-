using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace TicketSystem.Api.Services;

public class EmailReceiverService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailReceiverService> _logger;
    private readonly string _imapHost;
    private readonly int _imapPort;
    private readonly string _imapUser;
    private readonly string _imapPassword;
    private readonly int _pollIntervalSeconds;
    private ImapClient? _client;

    public EmailReceiverService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<EmailReceiverService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _imapHost = Environment.GetEnvironmentVariable("IMAP_HOST") ?? config["Email:Imap:Host"] ?? "";
        _imapPort = int.TryParse(Environment.GetEnvironmentVariable("IMAP_PORT") ?? config["Email:Imap:Port"], out var p) ? p : 993;
        _imapUser = Environment.GetEnvironmentVariable("IMAP_USER") ?? config["Email:Imap:User"] ?? "";
        _imapPassword = Environment.GetEnvironmentVariable("IMAP_PASSWORD") ?? config["Email:Imap:Password"] ?? "";
        _pollIntervalSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("EMAIL_POLL_INTERVAL_SECONDS") ?? config["Email:PollIntervalSeconds"], out var i) ? i : 60;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailReceiverService started. Polling interval: {Interval}s", _pollIntervalSeconds);

        if (string.IsNullOrWhiteSpace(_imapHost))
        {
            _logger.LogWarning("IMAP_HOST is not configured. Email receiving is DISABLED. Set IMAP_HOST, IMAP_USER, IMAP_PASSWORD environment variables or configure Email:Imap in appsettings.json.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureConnectedAsync(stoppingToken);
                await PollInboxAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email polling cycle failed. Retrying in {Interval}s...", _pollIntervalSeconds);
                await DisposeClientAsync();
            }

            await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken);
        }

        await DisposeClientAsync();
        _logger.LogInformation("EmailReceiverService stopped");
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_client?.IsConnected == true && _client?.IsAuthenticated == true) return;

        await DisposeClientAsync();
        _client = new ImapClient();
        _client.Timeout = 30000;
        _client.ServerCertificateValidationCallback = (object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
        {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            _logger.LogWarning("SSL certificate validation failed: {Errors}. Accepting anyway.", sslPolicyErrors);
            return true;
        };

        await _client.ConnectAsync(_imapHost, _imapPort, SecureSocketOptions.Auto, ct);
        await _client.AuthenticateAsync(_imapUser, _imapPassword, ct);
        _logger.LogInformation("Connected to IMAP server {Host}:{Port}", _imapHost, _imapPort);
    }

    private async Task DisposeClientAsync()
    {
        if (_client != null)
        {
            try { if (_client.IsConnected) await _client.DisconnectAsync(true); }
            catch { /* ignore cleanup errors */ }
            _client.Dispose();
            _client = null;
        }
    }

    private async Task PollInboxAsync(CancellationToken ct)
    {
        try
        {
            await _client!.Inbox.OpenAsync(FolderAccess.ReadWrite, ct);

            var uids = await _client.Inbox.SearchAsync(SearchQuery.NotSeen, ct);
            if (uids.Count == 0)
            {
                _logger.LogDebug("No unread emails found");
                return;
            }

            _logger.LogInformation("Found {Count} unread email(s)", uids.Count);

            foreach (var uid in uids)
            {
                try
                {
                    await ProcessEmailAsync(uid, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process email UID={Uid}", uid);
                }
            }
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex, "IMAP authentication failed for user {User}", _imapUser);
            throw;
        }
        catch (ImapProtocolException ex)
        {
            _logger.LogError(ex, "IMAP protocol error. Will retry on next cycle.");
            await DisposeClientAsync();
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IMAP connection error. Will retry on next cycle.");
            await DisposeClientAsync();
        }
    }

    private async Task ProcessEmailAsync(UniqueId uid, CancellationToken ct)
    {
        var message = await _client!.Inbox.GetMessageAsync(uid, ct);
        if (message == null)
        {
            _logger.LogWarning("Could not fetch message UID={Uid}", uid);
            return;
        }

        var fromAddress = message.From.Mailboxes.FirstOrDefault()?.Address ?? "";
        var fromName = message.From.Mailboxes.FirstOrDefault()?.Name ?? "";
        var subject = message.Subject ?? "";
        var body = GetEmailBody(message);
        var messageId = message.MessageId ?? Guid.NewGuid().ToString("N");
        var inReplyTo = message.InReplyTo;
        var references = string.Join(" ", message.References);

        _logger.LogInformation("Processing email from {From}: {Subject} (MessageId={MessageId})", fromAddress, subject, messageId);

        var attachments = new List<EmailAttachment>();
        foreach (var attachment in message.Attachments)
        {
            var emailAttachment = await ExtractAttachmentAsync(attachment);
            if (emailAttachment != null)
            {
                attachments.Add(emailAttachment);
            }
        }

        foreach (var related in message.BodyParts.OfType<MimePart>())
        {
            if (related.IsAttachment && !attachments.Any(a => a.FileName == related.FileName))
            {
                var emailAttachment = await ExtractAttachmentAsync(related);
                if (emailAttachment != null)
                {
                    attachments.Add(emailAttachment);
                }
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var processingService = scope.ServiceProvider.GetRequiredService<IEmailProcessingService>();

        try
        {
            await processingService.ProcessEmailAsync(fromAddress, fromName, subject, body,
                messageId, inReplyTo, references, attachments, ct);

            _client.Inbox.AddFlags(uid, MessageFlags.Seen, true, ct);
            _logger.LogInformation("Successfully processed email UID={Uid} from {From}", uid, fromAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process email UID={Uid} from {From}. Keeping as unread for retry.", uid, fromAddress);
        }
    }

    private static string GetEmailBody(MimeMessage message)
    {
        if (message.TextBody != null)
            return message.TextBody;

        if (message.HtmlBody != null)
            return StripHtml(message.HtmlBody);

        return message.Body?.ToString() ?? "";
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static async Task<EmailAttachment?> ExtractAttachmentAsync(MimeEntity entity)
    {
        try
        {
            if (entity is MimePart part)
            {
                var fileName = part.FileName ?? $"attachment_{Guid.NewGuid():N}";
                var contentType = part.ContentType?.MimeType ?? "application/octet-stream";

                if (part.Content == null) return null;
                using var memoryStream = new MemoryStream();
                await part.Content.DecodeToAsync(memoryStream);
                var data = memoryStream.ToArray();

                if (data.Length == 0) return null;

                return new EmailAttachment
                {
                    FileName = fileName,
                    ContentType = contentType,
                    Data = data
                };
            }

            if (entity is MessagePart rfc822)
            {
                var fileName = rfc822.ContentDisposition?.FileName ?? rfc822.ContentType?.Name ?? "forwarded_email.eml";
                using var memoryStream = new MemoryStream();
                await rfc822.WriteToAsync(memoryStream);
                var data = memoryStream.ToArray();

                return new EmailAttachment
                {
                    FileName = fileName,
                    ContentType = "message/rfc822",
                    Data = data
                };
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to extract attachment: {ex.Message}");
        }

        return null;
    }
}
