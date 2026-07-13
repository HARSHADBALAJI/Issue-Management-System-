namespace TicketSystem.Api.Models.DTOs.Applications;

public class CreateApplicationRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
