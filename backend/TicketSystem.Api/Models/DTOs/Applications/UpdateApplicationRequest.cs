namespace TicketSystem.Api.Models.DTOs.Applications;

public class UpdateApplicationRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
