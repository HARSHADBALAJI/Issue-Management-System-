namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TicketStatsQueryParams
{
    public int? ApplicationId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? UserId { get; set; }
}
