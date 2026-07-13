namespace TicketSystem.Api.Models.Entities;

public enum SlaAction
{
    Created,
    Started,
    Paused,
    Resumed,
    Completed,
    Breached,
    Reopened,
    PriorityChanged,
    NotificationSent,
    WorkingHoursAdjusted,
    HolidayAdjusted
}

public class SlaAuditLog
{
    public int Id { get; set; }
    public int TicketSlaId { get; set; }
    public int TicketId { get; set; }
    public SlaAction Action { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TicketSla TicketSla { get; set; } = null!;
    public Ticket Ticket { get; set; } = null!;
}
