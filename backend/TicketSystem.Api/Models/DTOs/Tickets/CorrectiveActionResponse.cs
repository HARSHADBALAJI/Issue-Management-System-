namespace TicketSystem.Api.Models.DTOs.Tickets;

public class CorrectiveActionResponse
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int PerformedByUserId { get; set; }
    public string PerformedByName { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
