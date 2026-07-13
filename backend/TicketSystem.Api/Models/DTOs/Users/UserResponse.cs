namespace TicketSystem.Api.Models.DTOs.Users;

public class UserResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<int> ApplicationIds { get; set; } = new();
    public List<string> AssignedApps { get; set; } = new();
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public class AssignedAppDto
    {
        public int ApplicationId { get; set; }
        public string ApplicationName { get; set; } = string.Empty;
        public bool IsPrimarySPOC { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
