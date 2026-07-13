namespace TicketSystem.Api.Models.DTOs.Applications;

public class ApplicationUsersResponse
{
    public int ApplicationId { get; set; }
    public List<int> UserIds { get; set; } = new();
}
