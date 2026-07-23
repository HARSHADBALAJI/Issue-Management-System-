namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TicketSlaSummary
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
    public DateTime? SlaDeadline { get; set; }
    public double Percentage { get; set; }
    public string? SlaStatus { get; set; }
    public string? TimeRemaining { get; set; }
}
