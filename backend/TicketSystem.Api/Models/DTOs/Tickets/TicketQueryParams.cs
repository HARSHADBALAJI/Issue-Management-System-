using TicketSystem.Api.Models.DTOs.Common;

namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TicketQueryParams : QueryParams
{
    public int? StatusId { get; set; }
    public int? ApplicationId { get; set; }
    public int? AssignedTo { get; set; }
    public int? RequesterId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
    public bool? Unassigned { get; set; }
    public string? Priority { get; set; }
    public bool? SlaBreached { get; set; }
}
