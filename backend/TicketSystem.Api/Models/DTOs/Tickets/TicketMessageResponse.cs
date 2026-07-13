namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TicketMessageResponse
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? RequesterId { get; set; }
    public string? RequesterName { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserRole { get; set; }
    public string MessageSourceType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AttachmentDto> Attachments { get; set; } = new();

    public class AttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
