using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TicketSystem.Api.Models.DTOs.Ocr;

namespace TicketSystem.Api.Services;

public class OcrService : IOcrService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OcrService> _logger;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly int _maxCallsPerMinute;
    private readonly int _maxImageBytes;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly CancellationTokenSource _disposeCts = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OcrService(IConfiguration config, ILogger<OcrService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();

        var apiKey = Environment.GetEnvironmentVariable("API_KEY")
            ?? config["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("API_KEY not configured");
        _endpoint = (Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? config["AzureOpenAI:Endpoint"]
            ?? "").TrimEnd('/');
        _apiKey = apiKey;
        _modelId = Environment.GetEnvironmentVariable("MODEL_ID")
            ?? config["AzureOpenAI:ModelId"]
            ?? "gpt-4o";
        _maxCallsPerMinute = int.TryParse(
            Environment.GetEnvironmentVariable("OPENAI_OCR_MAX_CALLS_PER_MINUTE")
            ?? config["OpenAI:OcrMaxCallsPerMinute"], out var m) ? m : 40;
        _maxImageBytes = int.TryParse(
            Environment.GetEnvironmentVariable("OPENAI_OCR_MAX_IMAGE_BYTES")
            ?? config["OpenAI:OcrMaxImageBytes"], out var b) ? b : 52428800;

        _rateLimiter = new SemaphoreSlim(_maxCallsPerMinute, _maxCallsPerMinute);
        _ = StartRateLimitResetAsync(_disposeCts.Token);

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
    }

    private async Task StartRateLimitResetAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
            var remaining = _maxCallsPerMinute - _rateLimiter.CurrentCount;
            if (remaining > 0)
            {
                _rateLimiter.Release(remaining);
            }
        }
    }

    public async Task<OcrExtractionResult> ExtractTicketInfoAsync(OcrRequest request)
    {
        await _rateLimiter.WaitAsync(_disposeCts.Token);

        try
        {
            var messages = new List<object>();
            var systemMessage = new
            {
                role = "system",
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = @"You are a ticket extraction assistant. Extract structured ticket information from the given email subject, body, and attachments.

Return ONLY valid JSON with these fields:
- subject: the email subject line (required; use original subject as-is)
- description: the email body text (required; use the exact body content, do NOT summarize or rephrase)
- application: the application or system name if clearly identifiable, otherwise ""Unknown""
- priority: one of ""low"", ""medium"", ""high"", ""critical"" (use ""medium"" if uncertain)
- requesterName: requester full name if found in signature/body, otherwise """" (empty)
- requesterEmail: requester email if found, otherwise """" (empty)
- attachmentsSummary: brief factual summary of attachment contents if readable, otherwise """" (empty)
- confidence: number 0-1 indicating extraction confidence

CRITICAL RULES - Read carefully:
1. NEVER make up or guess any information. Only extract what is actually present.
2. NEVER return text like ""No detailed description provided"", ""No subject"", ""N/A"", or any placeholder text. If data is absent, use the original value or empty string.
3. For description: use the EXACT email body text. Do NOT rephrase, summarize, or fabricate descriptions. If the body is short (e.g. ""zoho"", ""SAP issue"", ""Need help""), use it as-is.
4. For subject: use the original subject line as-is. If empty, use empty string.
5. If the application cannot be identified with high confidence, set it to ""Unknown"".
6. If confidence is below 0.70, set application to ""Unknown"".
7. Extract requester info ONLY from the email signature or body. Do NOT invent names or emails.
8. If the email body is empty, set description to empty string.
9. If the email has only attachments with no body text, describe them factually in attachmentsSummary.
10. If the email is a reply or forward, extract from the quoted content as-is.
11. For HTML-only emails, extract visible text only — do NOT include HTML tags or styling.
12. If you cannot understand the email at all, return the original subject and body as-is with application ""Unknown"".
13. Do NOT add any text outside the JSON object."                    }
                }
            };
            messages.Add(systemMessage);

            var userContent = new List<object>
            {
                new
                {
                    type = "text",
                    text = $"Subject: {request.Subject}\n\nBody:\n{request.EmailBody}"
                }
            };

            foreach (var attachment in request.Attachments)
            {
                if (attachment.Data.Length > _maxImageBytes)
                {
                    _logger.LogWarning("Attachment {FileName} exceeds max size ({Size} > {Max}), skipping",
                        attachment.FileName, attachment.Data.Length, _maxImageBytes);
                    continue;
                }

                if (IsImageContentType(attachment.ContentType))
                {
                    var base64 = Convert.ToBase64String(attachment.Data);
                    var mimeType = attachment.ContentType;
                    userContent.Add(new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = $"data:{mimeType};base64,{base64}",
                            detail = "auto"
                        }
                    });
                }
                else
                {
                    var text = ExtractTextContent(attachment);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        userContent.Add(new
                        {
                            type = "text",
                            text = $"\n--- Attachment: {attachment.FileName} ---\n{text}\n--- End of {attachment.FileName} ---"
                        });
                    }
                }
            }

            messages.Add(new { role = "user", content = userContent });

            var requestBody = new
            {
                model = _modelId,
                messages,
                max_tokens = 4096,
                temperature = 0.1,
                response_format = new { type = "json_object" }
            };

            var apiVersion = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION")
                ?? "2024-12-01-preview";
            var url = $"{_endpoint}/openai/deployments/{_modelId}/chat/completions?api-version={apiVersion}";

            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, _disposeCts.Token);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var messageContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(messageContent))
            {
                _logger.LogWarning("GPT-4o returned empty content");
                return CreateFallbackResult(request);
            }

            var result = JsonSerializer.Deserialize<OcrExtractionResult>(messageContent, JsonOptions);
            if (result == null)
            {
                _logger.LogWarning("Failed to deserialize GPT-4o response: {Response}", messageContent);
                return CreateFallbackResult(request);
            }

            // Sanitize AI response - never allow placeholder/fabricated content
            SanitizeResult(result, request);

            // Keep AI's original application name - confidence-based filtering
            // is handled by EmailProcessingService against the database.
            if (string.IsNullOrWhiteSpace(result.Priority))
                result.Priority = "medium";
            if (string.IsNullOrWhiteSpace(result.Application))
                result.Application = "Unknown";

            _logger.LogInformation("OCR extraction complete. App={App}, Priority={Priority}, Confidence={Confidence}",
                result.Application, result.Priority, result.Confidence);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure OpenAI API call failed for GPT-4o extraction");
            return CreateFallbackResult(request);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Azure OpenAI response");
            return CreateFallbackResult(request);
        }
    }

    private OcrExtractionResult CreateFallbackResult(OcrRequest request)
    {
        return new OcrExtractionResult
        {
            Subject = request.Subject ?? "",
            Description = request.EmailBody ?? "",
            Application = "Unknown",
            Priority = "medium",
            RequesterName = "",
            RequesterEmail = "",
            AttachmentsSummary = "",
            Confidence = 0.0
        };
    }

    private static void SanitizeResult(OcrExtractionResult result, OcrRequest request)
    {
        var placeholderPatterns = new[]
        {
            "no detailed description provided",
            "no description provided",
            "no subject",
            "n/a",
            "not available",
            "not applicable",
            "unavailable",
            "see attachment",
            "see attached",
            "see email",
            "see above"
        };

        // If description contains placeholder text, use original body instead
        if (!string.IsNullOrWhiteSpace(result.Description))
        {
            var descLower = result.Description.Trim().ToLowerInvariant();
            var isPlaceholder = placeholderPatterns.Any(p => descLower == p);
            if (isPlaceholder)
            {
                result.Description = request.EmailBody ?? "";
            }
        }

        // If description is empty but we have a body, use it
        if (string.IsNullOrWhiteSpace(result.Description) && !string.IsNullOrWhiteSpace(request.EmailBody))
        {
            result.Description = request.EmailBody;
        }

        // If subject is empty but we have one, use original
        if (string.IsNullOrWhiteSpace(result.Subject) && !string.IsNullOrWhiteSpace(request.Subject))
        {
            result.Subject = request.Subject;
        }

        // If requesterName is placeholder-like, clear it
        if (!string.IsNullOrWhiteSpace(result.RequesterName))
        {
            var nameLower = result.RequesterName.Trim().ToLowerInvariant();
            if (placeholderPatterns.Any(p => nameLower == p))
                result.RequesterName = "";
        }

        // If requesterEmail is placeholder-like, clear it
        if (!string.IsNullOrWhiteSpace(result.RequesterEmail))
        {
            var emailLower = result.RequesterEmail.Trim().ToLowerInvariant();
            if (placeholderPatterns.Any(p => emailLower == p) || !emailLower.Contains('@'))
                result.RequesterEmail = "";
        }

        // If attachmentsSummary is placeholder-like, clear it
        if (!string.IsNullOrWhiteSpace(result.AttachmentsSummary))
        {
            var summaryLower = result.AttachmentsSummary.Trim().ToLowerInvariant();
            if (placeholderPatterns.Any(p => summaryLower == p))
                result.AttachmentsSummary = "";
        }
    }

    private static bool IsImageContentType(string contentType)
    {
        return contentType switch
        {
            "image/png" => true,
            "image/jpeg" => true,
            "image/jpg" => true,
            "image/gif" => true,
            "image/bmp" => true,
            "image/webp" => true,
            "image/tiff" => true,
            _ => false
        };
    }

    private static string ExtractTextContent(Models.DTOs.Ocr.OcrAttachment attachment)
    {
        try
        {
            var contentType = attachment.ContentType?.ToLowerInvariant() ?? "";
            if (contentType.Contains("text/plain") || contentType.Contains("text/html"))
            {
                return Encoding.UTF8.GetString(attachment.Data);
            }

            if (contentType.Contains("application/pdf"))
            {
                return $"[PDF attachment: {attachment.FileName}, Size: {attachment.Data.Length} bytes]";
            }

            if (contentType.Contains("word") || contentType.Contains("docx") || contentType.Contains("doc"))
            {
                return $"[Document attachment: {attachment.FileName}, Size: {attachment.Data.Length} bytes]";
            }

            return $"[Attachment: {attachment.FileName}, Type: {attachment.ContentType}, Size: {attachment.Data.Length} bytes]";
        }
        catch
        {
            return $"[Attachment: {attachment.FileName}, could not extract text]";
        }
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _rateLimiter.Dispose();
        _httpClient.Dispose();
    }
}
