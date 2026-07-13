namespace TicketSystem.Api.Models.Entities;

public class SlaPolicy
{
    public int Id { get; set; }
    public string Priority { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
