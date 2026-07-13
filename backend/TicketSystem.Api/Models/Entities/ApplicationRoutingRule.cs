namespace TicketSystem.Api.Models.Entities;

public class ApplicationRoutingRule
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public int DepartmentId { get; set; }
    public int PrimarySpocUserId { get; set; }
    public int? BackupSpocUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Application Application { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public User PrimarySpocUser { get; set; } = null!;
    public User? BackupSpocUser { get; set; }
}
