namespace TicketSystem.Api.Services;

public interface IEmailProcessingService
{
    Task ProcessEmailAsync(string fromAddress, string fromName, string subject, string body,
        string messageId, string? inReplyTo, string? references,
        List<EmailAttachment> attachments, CancellationToken ct = default);
}

public class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
