namespace TicketSystem.Api.Models.Entities;

public class TicketStatus
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<TicketStatusHistory> StatusHistoriesAsFrom { get; set; } = new List<TicketStatusHistory>();
    public ICollection<TicketStatusHistory> StatusHistoriesAsTo { get; set; } = new List<TicketStatusHistory>();
}
