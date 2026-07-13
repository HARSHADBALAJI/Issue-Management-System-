namespace TicketSystem.Api.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Department Department { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<ApplicationAssignment> ApplicationAssignments { get; set; } = new List<ApplicationAssignment>();
    public ICollection<TicketStatusHistory> StatusHistories { get; set; } = new List<TicketStatusHistory>();
    public ICollection<TicketCorrectiveAction> CorrectiveActions { get; set; } = new List<TicketCorrectiveAction>();
    public ICollection<EmailMessage> EmailMessages { get; set; } = new List<EmailMessage>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
