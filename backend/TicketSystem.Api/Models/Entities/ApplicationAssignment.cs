namespace TicketSystem.Api.Models.Entities;

public class ApplicationAssignment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ApplicationId { get; set; }
    public bool IsPrimarySPOC { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Application Application { get; set; } = null!;
}
