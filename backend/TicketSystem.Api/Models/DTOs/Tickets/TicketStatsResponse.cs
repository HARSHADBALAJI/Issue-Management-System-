namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TicketStatsResponse
{
    public int Total { get; set; }
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int Waiting { get; set; }
    public int Resolved { get; set; }
    public int Closed { get; set; }
    public int SlaBreached { get; set; }
    public string AvgResolutionTime { get; set; } = string.Empty;
    public double SlaCompliance { get; set; }
    public List<PriorityDistItem> PriorityDistribution { get; set; } = new();

    public class PriorityDistItem
    {
        public string Priority { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
