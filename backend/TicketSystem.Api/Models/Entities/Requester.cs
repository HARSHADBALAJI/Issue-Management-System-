namespace TicketSystem.Api.Models.Entities;

public class Requester
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Company { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketStatusHistory> StatusHistories { get; set; } = new List<TicketStatusHistory>();
    public ICollection<EmailMessage> EmailMessages { get; set; } = new List<EmailMessage>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
