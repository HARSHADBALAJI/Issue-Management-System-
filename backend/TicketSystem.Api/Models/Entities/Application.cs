namespace TicketSystem.Api.Models.Entities;

public class Application
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ApplicationAssignment> ApplicationAssignments { get; set; } = new List<ApplicationAssignment>();
    public ICollection<ApplicationRoutingRule> RoutingRules { get; set; } = new List<ApplicationRoutingRule>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
