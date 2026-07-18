namespace TicketSystem.Api.Services;

public class InlineAttachmentInfo
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

public interface IEmailOutboxService
{
    Task EnqueueAsync(string recipientEmail, string subject, string bodyHtml, string? bodyPlainText,
        int? ticketId = null, int? requesterId = null, int? userId = null,
        string? inReplyTo = null, string? references = null,
        string? senderEmail = null, string? recipientName = null,
        int? ticketMessageId = null,
        List<InlineAttachmentInfo>? inlineAttachments = null);
}
