namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TicketListResponse
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public int ApplicationId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime? SlaDeadline { get; set; }
    public bool IsSlaBreached { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
