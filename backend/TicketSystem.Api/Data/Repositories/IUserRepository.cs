using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Users;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<PagedResponse<UserResponse>> GetPagedAsync(UserQueryParams query);
    Task<UserResponse?> GetUserResponseAsync(int id);
    Task<User?> GetByEmailAsync(string email);
    Task<List<LookupItem>> GetLookupAsync(int? roleId = null);
    Task<List<int>> GetApplicationIdsAsync(int userId);
    Task UpdateApplicationsAsync(int userId, List<int> applicationIds);
    Task UpdateRoutingRulesForUserAsync(int userId, int oldDepartmentId, int newDepartmentId);
}
