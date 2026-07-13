using TicketSystem.Api.Models.DTOs.Ocr;

namespace TicketSystem.Api.Services;

public interface IOcrService
{
    Task<OcrExtractionResult> ExtractTicketInfoAsync(OcrRequest request);
}
