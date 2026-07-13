namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TicketStatusHistoryResponse
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? FromStatusId { get; set; }
    public string? FromStatusName { get; set; }
    public int ToStatusId { get; set; }
    public string ToStatusName { get; set; } = string.Empty;
    public int? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}
