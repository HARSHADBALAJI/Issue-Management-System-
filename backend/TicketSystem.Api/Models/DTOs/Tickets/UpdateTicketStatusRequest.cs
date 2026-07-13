namespace TicketSystem.Api.Models.DTOs.Tickets;

public class UpdateTicketStatusRequest
{
    public int StatusId { get; set; }
    public string? Remarks { get; set; }
}
