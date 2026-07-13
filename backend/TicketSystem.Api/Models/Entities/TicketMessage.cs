namespace TicketSystem.Api.Models.Entities;

public class TicketMessage
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? RequesterId { get; set; }
    public int? UserId { get; set; }
    public string MessageSourceType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public string? InReplyTo { get; set; }
    public string? References { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public Requester? Requester { get; set; }
    public User? User { get; set; }
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}
