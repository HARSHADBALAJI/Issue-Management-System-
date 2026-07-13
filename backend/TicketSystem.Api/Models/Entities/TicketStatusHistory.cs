namespace TicketSystem.Api.Models.Entities;

public class TicketStatusHistory
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? FromStatusId { get; set; }
    public int ToStatusId { get; set; }
    public int? ChangedByUserId { get; set; }
    public int? ChangedByRequesterId { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public TicketStatus? FromStatus { get; set; }
    public TicketStatus ToStatus { get; set; } = null!;
    public User? ChangedByUser { get; set; }
    public Requester? ChangedByRequester { get; set; }
}
