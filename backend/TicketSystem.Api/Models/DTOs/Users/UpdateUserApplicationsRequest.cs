namespace TicketSystem.Api.Models.DTOs.Users;

public class UpdateUserApplicationsRequest
{
    public List<int> ApplicationIds { get; set; } = new();
}
