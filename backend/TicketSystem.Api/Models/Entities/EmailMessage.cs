namespace TicketSystem.Api.Models.Entities;

public class EmailMessage
{
    public int Id { get; set; }
    public int? TicketId { get; set; }
    public int? RequesterId { get; set; }
    public int? UserId { get; set; }
    public string? MessageId { get; set; }
    public string? InReplyTo { get; set; }
    public string? References { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
    public Requester? Requester { get; set; }
    public User? User { get; set; }
}
