using TicketSystem.Api.Models.DTOs.Applications;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public interface IApplicationRepository : IRepository<Application>
{
    Task<PagedResponse<ApplicationResponse>> GetPagedAsync(ApplicationQueryParams query);
    Task<ApplicationResponse?> GetApplicationResponseAsync(int id);
    Task<List<LookupItem>> GetLookupAsync();
    Task UpdateUsersAsync(int applicationId, List<int> userIds, int? primarySpocUserId);
    Task<int> GetTicketCountAsync(int applicationId);
    Task HardDeleteAsync(int id);
}
