namespace TicketSystem.Api.Models.DTOs.Departments;

public class DepartmentResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? HeadUserId { get; set; }
    public string? HeadName { get; set; }
    public int UserCount { get; set; }
    public int SpocCount { get; set; }
    public int AdminCount { get; set; }
    public bool IsActive { get; set; }
}
