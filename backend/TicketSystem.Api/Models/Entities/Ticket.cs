namespace TicketSystem.Api.Models.Entities;

public class Ticket
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public int RequesterId { get; set; }
    public int ApplicationId { get; set; }
    public int? AssignedToUserId { get; set; }
    public int StatusId { get; set; } = 1;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public DateTime? SlaDeadline { get; set; }
    public DateTime? SlaBreachedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Requester Requester { get; set; } = null!;
    public Application Application { get; set; } = null!;
    public User? AssignedToUser { get; set; }
    public TicketStatus Status { get; set; } = null!;
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketStatusHistory> StatusHistory { get; set; } = new List<TicketStatusHistory>();
    public ICollection<TicketCorrectiveAction> CorrectiveActions { get; set; } = new List<TicketCorrectiveAction>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<EmailMessage> EmailMessages { get; set; } = new List<EmailMessage>();
    public ICollection<TicketSla> TicketSlas { get; set; } = new List<TicketSla>();
}
