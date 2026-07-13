namespace TicketSystem.Api.Models.DTOs.Tickets;

public class BulkResponse
{
    public int UpdatedCount { get; set; }
    public List<TicketResponse> Tickets { get; set; } = new();
}
