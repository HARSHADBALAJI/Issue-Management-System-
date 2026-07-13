namespace TicketSystem.Api.Models.DTOs.Applications;

public class ApplicationResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int AssignedUserCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<AssignedUserDto> AssignedUsers { get; set; } = new();

    public class AssignedUserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsPrimarySPOC { get; set; }
    }
}
