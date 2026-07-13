using TicketSystem.Api.Models.DTOs.Applications;
using TicketSystem.Api.Models.DTOs.Common;

namespace TicketSystem.Api.Services;

public interface IApplicationService
{
    Task<PagedResponse<ApplicationResponse>> GetPagedAsync(ApplicationQueryParams query);
    Task<ApplicationResponse?> GetByIdAsync(int id);
    Task<ApplicationResponse> CreateAsync(CreateApplicationRequest request);
    Task<ApplicationResponse> UpdateAsync(int id, UpdateApplicationRequest request);
    Task DeleteAsync(int id);
    Task<List<LookupItem>> GetLookupAsync();
    Task UpdateUsersAsync(int applicationId, List<int> userIds, int? primarySpocUserId = null);
}
