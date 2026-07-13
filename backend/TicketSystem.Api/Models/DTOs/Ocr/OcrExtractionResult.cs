namespace TicketSystem.Api.Models.DTOs.Ocr;

public class OcrExtractionResult
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Application { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string AttachmentsSummary { get; set; } = string.Empty;
    public double Confidence { get; set; }
}
