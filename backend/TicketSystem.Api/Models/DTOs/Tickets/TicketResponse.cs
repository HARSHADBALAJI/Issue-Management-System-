namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TicketResponse
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public int RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public int ApplicationId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public string ApplicationAlias { get; set; } = string.Empty;
    public int? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool IsSlaBreached { get; set; }
    public string SlaConsumed { get; set; } = string.Empty;
    public string SlaRemaining { get; set; } = string.Empty;
}
