// ================================================================
// Gmail REST API-based Email Receiver (HTTPS / port 443)
// Replaces the old IMAP-based receiver that was blocked by firewall.
//
// Uses raw HttpClient + OAuth2 refresh token to call Gmail API.
// No complex Google SDK credential classes needed.
//
// Required env vars:
//   GMAIL_CLIENT_ID      - OAuth2 client ID
//   GMAIL_CLIENT_SECRET  - OAuth2 client secret
//   GMAIL_REFRESH_TOKEN  - OAuth2 refresh token
//   GMAIL_USER_EMAIL     - Gmail address to read from
//   EMAIL_POLL_INTERVAL_SECONDS - Polling interval (default: 10)
// ================================================================

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TicketSystem.Api.Services;

public class EmailReceiverService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailReceiverService> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _refreshToken;
    private readonly string _userEmail;
    private readonly int _pollIntervalSeconds;
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public EmailReceiverService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<EmailReceiverService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _http = new HttpClient();

        _clientId = Environment.GetEnvironmentVariable("GMAIL_CLIENT_ID") ?? "";
        _clientSecret = Environment.GetEnvironmentVariable("GMAIL_CLIENT_SECRET") ?? "";
        _refreshToken = Environment.GetEnvironmentVariable("GMAIL_REFRESH_TOKEN") ?? "";
        _userEmail = Environment.GetEnvironmentVariable("GMAIL_USER_EMAIL")
                     ?? Environment.GetEnvironmentVariable("IMAP_USER")
                     ?? config["Email:Imap:User"]
                     ?? "";

        _pollIntervalSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("EMAIL_POLL_INTERVAL_SECONDS")
            ?? config["Email:PollIntervalSeconds"], out var i) ? i : 10;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "EmailReceiverService [Gmail REST API] started. Poll={Interval}s User={User}",
            _pollIntervalSeconds, _userEmail);

        if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret) || string.IsNullOrWhiteSpace(_refreshToken))
        {
            _logger.LogWarning(
                "Gmail OAuth2 credentials not configured. Email receiving DISABLED. " +
                "Set GMAIL_CLIENT_ID, GMAIL_CLIENT_SECRET, GMAIL_REFRESH_TOKEN in .env");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureAccessTokenAsync(stoppingToken);
                await PollInboxAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email polling cycle failed. Retrying in {Interval}s...", _pollIntervalSeconds);
                _accessToken = null; // Force token refresh
            }

            await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("EmailReceiverService stopped");
    }

    // ── OAuth2 Token Refresh ──────────────────────────────────────

    private async Task EnsureAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-1))
            return;

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["refresh_token"] = _refreshToken,
            ["grant_type"] = "refresh_token"
        };

        var resp = await _http.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(form), ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _accessToken = root.GetProperty("access_token").GetString()!;
        var expiresIn = root.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

        _logger.LogDebug("Access token refreshed, expires in {Seconds}s", expiresIn);
    }

    private HttpRequestMessage GmailRequest(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return req;
    }

    // ── Inbox Polling ─────────────────────────────────────────────

    private async Task PollInboxAsync(CancellationToken ct)
    {
        var url = $"https://gmail.googleapis.com/gmail/v1/users/{_userEmail}/messages?q=is:unread+in:inbox&maxResults=20";
        var resp = await _http.SendAsync(GmailRequest(url), ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("messages", out var messages) || messages.GetArrayLength() == 0)
        {
            _logger.LogDebug("No unread emails found");
            return;
        }

        _logger.LogInformation("Found {Count} unread email(s)", messages.GetArrayLength());

        foreach (var msgRef in messages.EnumerateArray())
        {
            var messageId = msgRef.GetProperty("id").GetString()!;
            try
            {
                await ProcessMessageAsync(messageId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message {MessageId}", messageId);
            }
        }
    }

    // ── Single Message Processing ─────────────────────────────────

    private async Task ProcessMessageAsync(string messageId, CancellationToken ct)
    {
        // Fetch full message
        var url = $"https://gmail.googleapis.com/gmail/v1/users/{_userEmail}/messages/{messageId}?format=full";
        var resp = await _http.SendAsync(GmailRequest(url), ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var payload = root.GetProperty("payload");

        // ── Extract headers ──
        string GetHeader(string name)
        {
            if (!payload.TryGetProperty("headers", out var headers)) return "";
            foreach (var h in headers.EnumerateArray())
            {
                if (h.TryGetProperty("name", out var nameProp) &&
                    nameProp.GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true &&
                    h.TryGetProperty("value", out var valProp))
                    return valProp.GetString() ?? "";
            }
            return "";
        }

        var fromRaw = GetHeader("From");
        var subject = GetHeader("Subject");
        var inReplyTo = GetHeader("In-Reply-To");
        var references = GetHeader("References");
        var messageIdHeader = GetHeader("Message-Id");
        if (string.IsNullOrWhiteSpace(messageIdHeader))
            messageIdHeader = root.TryGetProperty("threadId", out var tid) ? tid.GetString()! : messageId;

        var fromAddress = ExtractEmail(fromRaw);
        var fromName = ExtractName(fromRaw);

        _logger.LogInformation("Processing email from {From}: {Subject} (Id={MessageId})",
            fromAddress, subject, messageIdHeader);

        // ── Extract body ──
        var body = ExtractBody(payload);

        // ── Extract attachments ──
        var attachments = new List<EmailAttachment>();
        var attachmentFetches = new List<(string attId, EmailAttachment att)>();
        CollectAttachments(payload, attachments, attachmentFetches);

        // ── Fetch attachment data ──
        foreach (var (attId, att) in attachmentFetches)
        {
            try
            {
                var attUrl = $"https://gmail.googleapis.com/gmail/v1/users/{_userEmail}/messages/{messageId}/attachments/{attId}";
                var attResp = await _http.SendAsync(GmailRequest(attUrl), ct);
                if (attResp.IsSuccessStatusCode)
                {
                    var attJson = await attResp.Content.ReadAsStringAsync(ct);
                    using var attDoc = JsonDocument.Parse(attJson);
                    var attData = attDoc.RootElement.GetProperty("data").GetString() ?? "";
                    att.Data = Convert.FromBase64String(attData.Replace('-', '+').Replace('_', '/'));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch attachment {Name}", att.FileName);
            }
        }

        // ── Delegate to processing service ──
        using var scope = _scopeFactory.CreateScope();
        var processingService = scope.ServiceProvider.GetRequiredService<IEmailProcessingService>();

        try
        {
            await processingService.ProcessEmailAsync(
                fromAddress, fromName, subject, body,
                messageIdHeader,
                string.IsNullOrWhiteSpace(inReplyTo) ? null : inReplyTo,
                string.IsNullOrWhiteSpace(references) ? null : references,
                attachments, ct);

            // Mark as read — remove UNREAD label
            var modUrl = $"https://gmail.googleapis.com/gmail/v1/users/{_userEmail}/messages/{messageId}/modify";
            var modBody = JsonSerializer.Serialize(new { removeLabelIds = new[] { "UNREAD" } });
            var modReq = new HttpRequestMessage(HttpMethod.Post, modUrl)
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _accessToken) },
                Content = new StringContent(modBody, Encoding.UTF8, "application/json")
            };
            await _http.SendAsync(modReq, ct);

            _logger.LogInformation("Successfully processed email from {From}: {Subject}", fromAddress, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process email from {From}. Keeping unread for retry.", fromAddress);
        }
    }

    // ── Body Extraction ───────────────────────────────────────────

    private static string ExtractBody(JsonElement part)
    {
        // Direct body
        if (part.TryGetProperty("body", out var body) && body.TryGetProperty("data", out var data))
        {
            var mimeType = part.TryGetProperty("mimeType", out var mt) ? mt.GetString() ?? "" : "";
            if (mimeType == "text/plain" || mimeType == "text/html")
                return DecodeBase64Url(data.GetString()!);
        }

        // Multipart children
        if (part.TryGetProperty("parts", out var parts))
        {
            // Prefer text/plain
            foreach (var child in parts.EnumerateArray())
            {
                var mime = child.TryGetProperty("mimeType", out var mt) ? mt.GetString() ?? "" : "";
                if (mime == "text/plain" && child.TryGetProperty("body", out var b) && b.TryGetProperty("data", out var d))
                    return DecodeBase64Url(d.GetString()!);
            }
            // Fallback to recursion
            foreach (var child in parts.EnumerateArray())
            {
                var result = ExtractBody(child);
                if (!string.IsNullOrEmpty(result))
                    return result;
            }
        }

        return "";
    }

    // ── Attachment Extraction ─────────────────────────────────────

    private static void CollectAttachments(JsonElement part,
        List<EmailAttachment> attachments, List<(string attId, EmailAttachment att)> toFetch)
    {
        if (part.TryGetProperty("body", out var body) &&
            body.TryGetProperty("attachmentId", out var attIdProp))
        {
            var attId = attIdProp.GetString()!;
            var fileName = part.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "attachment" : "attachment";
            var mimeType = part.TryGetProperty("mimeType", out var mt) ? mt.GetString() ?? "application/octet-stream" : "application/octet-stream";

            if (!string.IsNullOrEmpty(fileName) && fileName != "attachment")
            {
                var att = new EmailAttachment
                {
                    FileName = fileName,
                    ContentType = mimeType,
                    Data = Array.Empty<byte>()
                };
                attachments.Add(att);
                toFetch.Add((attId, att));
            }
        }

        if (part.TryGetProperty("parts", out var parts))
        {
            foreach (var child in parts.EnumerateArray())
            {
                CollectAttachments(child, attachments, toFetch);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static string ExtractEmail(string fromRaw)
    {
        var lt = fromRaw.IndexOf('<');
        var gt = fromRaw.IndexOf('>');
        if (lt >= 0 && gt > lt)
            return fromRaw.Substring(lt + 1, gt - lt - 1).Trim();
        return fromRaw.Trim();
    }

    private static string ExtractName(string fromRaw)
    {
        var lt = fromRaw.IndexOf('<');
        if (lt > 0)
            return fromRaw.Substring(0, lt).Trim().Trim('"');
        return "";
    }

    private static string DecodeBase64Url(string data)
    {
        var padded = data.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(padded)); }
        catch { return ""; }
    }
}
