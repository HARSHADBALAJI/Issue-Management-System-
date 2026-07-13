namespace TicketSystem.Api.Models.DTOs.Departments;

public class UpdateDepartmentRequest
{
    public string Name { get; set; } = string.Empty;
    public string? HeadName { get; set; }
}
