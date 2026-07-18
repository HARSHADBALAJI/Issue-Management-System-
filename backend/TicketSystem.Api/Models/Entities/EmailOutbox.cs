namespace TicketSystem.Api.Models.Entities;

public class EmailOutbox
{
    public int Id { get; set; }
    public int? TicketId { get; set; }
    public int? RequesterId { get; set; }
    public int? UserId { get; set; }
    public int? TicketMessageId { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? BodyPlainText { get; set; }

    public string? InReplyTo { get; set; }
    public string? References { get; set; }
    public string? InlineAttachmentsJson { get; set; }

    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 6;
    public string? LastError { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? SentMessageId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    public Ticket? Ticket { get; set; }
    public Requester? Requester { get; set; }
    public User? User { get; set; }
}
