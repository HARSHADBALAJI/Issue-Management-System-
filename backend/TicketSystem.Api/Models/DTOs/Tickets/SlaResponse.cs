namespace TicketSystem.Api.Models.DTOs.Tickets;

public class SlaResponse
{
    public string Target { get; set; } = "4h";
    public string Consumed { get; set; } = string.Empty;
    public string Paused { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public bool IsSlaBreached { get; set; }
    public string? EscalationLevel { get; set; }
    public DateTime? BreachedAt { get; set; }
}
