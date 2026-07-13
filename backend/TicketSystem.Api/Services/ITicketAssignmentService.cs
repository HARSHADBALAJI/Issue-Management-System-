namespace TicketSystem.Api.Services;

public interface ITicketAssignmentService
{
    Task<int?> GetPrimarySpocUserIdAsync(int applicationId);
}
