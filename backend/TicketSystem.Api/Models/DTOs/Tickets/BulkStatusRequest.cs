namespace TicketSystem.Api.Models.DTOs.Tickets;

public class BulkStatusRequest
{
    public List<int> TicketIds { get; set; } = new();
    public int StatusId { get; set; }
}
