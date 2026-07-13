namespace TicketSystem.Api.Models.DTOs.Users;

public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string Role { get; set; } = "spoc";
    public string Status { get; set; } = "active";
}