namespace TicketSystem.Api.Models.DTOs.Tickets;

public class AgentPerformanceResponse
{
    public int AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public int Assigned { get; set; }
    public int Resolved { get; set; }
    public int Open { get; set; }
    public double SlaPercentage { get; set; }
    public string AvgResolutionTime { get; set; } = string.Empty;
}
