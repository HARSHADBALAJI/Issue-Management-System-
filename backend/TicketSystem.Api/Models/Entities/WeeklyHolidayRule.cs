namespace TicketSystem.Api.Models.Entities;

public class WeeklyHolidayRule
{
    public int Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string WeekType { get; set; } = "All";
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
