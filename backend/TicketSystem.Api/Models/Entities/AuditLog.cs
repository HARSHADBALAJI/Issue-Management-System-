namespace TicketSystem.Api.Models.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public int? RequesterId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Requester? Requester { get; set; }
}
