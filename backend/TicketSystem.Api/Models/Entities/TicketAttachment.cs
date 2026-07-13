namespace TicketSystem.Api.Models.Entities;

public class TicketAttachment
{
    public int Id { get; set; }
    public int TicketMessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public byte[] FileData { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TicketMessage TicketMessage { get; set; } = null!;
}
