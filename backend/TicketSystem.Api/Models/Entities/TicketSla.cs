namespace TicketSystem.Api.Models.Entities;

public enum SlaStatus
{
    NotStarted,
    Running,
    Paused,
    Completed,
    Breached,
    Reopened
}

public class TicketSla
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int SlaPolicyId { get; set; }
    public string Priority { get; set; } = string.Empty;
    public SlaStatus Status { get; set; } = SlaStatus.NotStarted;
    public DateTime? StartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public TimeSpan TotalPausedDuration { get; set; } = TimeSpan.Zero;
    public DateTime DeadlineAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? BreachedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public SlaPolicy SlaPolicy { get; set; } = null!;
}
