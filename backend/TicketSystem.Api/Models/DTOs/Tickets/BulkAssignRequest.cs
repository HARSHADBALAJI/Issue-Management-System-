namespace TicketSystem.Api.Models.DTOs.Tickets;

public class BulkAssignRequest
{
    public List<int> TicketIds { get; set; } = new();
    public int AssignedToUserId { get; set; }
}
