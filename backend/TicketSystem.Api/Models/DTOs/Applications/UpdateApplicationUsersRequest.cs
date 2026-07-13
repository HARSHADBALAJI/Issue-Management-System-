namespace TicketSystem.Api.Models.DTOs.Applications;

public class UpdateApplicationUsersRequest
{
    public List<int> UserIds { get; set; } = new();
    public int? PrimarySpocUserId { get; set; }
}
