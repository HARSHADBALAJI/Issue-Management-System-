namespace TicketSystem.Api.Models.DTOs.Tickets;

public class CreateCorrectiveActionRequest
{
    public string Description { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; }
}
