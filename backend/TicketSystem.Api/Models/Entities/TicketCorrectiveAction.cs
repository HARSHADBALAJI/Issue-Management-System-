namespace TicketSystem.Api.Models.Entities;

public class TicketCorrectiveAction
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int PerformedByUserId { get; set; }
    public DateTime PerformedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User PerformedByUser { get; set; } = null!;
}
