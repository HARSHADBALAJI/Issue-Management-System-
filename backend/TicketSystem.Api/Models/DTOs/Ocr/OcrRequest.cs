namespace TicketSystem.Api.Models.DTOs.Ocr;

public class OcrRequest
{
    public string EmailBody { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public List<OcrAttachment> Attachments { get; set; } = new();
}

public class OcrAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
