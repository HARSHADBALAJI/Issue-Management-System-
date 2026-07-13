namespace TicketSystem.Api.Models.Entities;

public class SlaSetting
{
    public int Id { get; set; }
    public TimeSpan WorkStartTime { get; set; } = new(9, 0, 0);
    public TimeSpan WorkEndTime { get; set; } = new(17, 40, 0);
    public int NotifyBeforeHours { get; set; } = 24;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
