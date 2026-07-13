namespace TicketSystem.Api.Models.DTOs.Users;

public class UserApplicationsResponse
{
    public int UserId { get; set; }
    public List<int> ApplicationIds { get; set; } = new();
}
