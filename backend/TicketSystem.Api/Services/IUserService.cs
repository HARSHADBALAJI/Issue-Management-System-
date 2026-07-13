using TicketSystem.Api.Models.DTOs.Auth;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Users;

namespace TicketSystem.Api.Services;

public interface IUserService
{
    Task<PagedResponse<UserResponse>> GetPagedAsync(UserQueryParams query);
    Task<UserResponse?> GetByIdAsync(int id);
    Task<UserResponse> CreateAsync(CreateUserRequest request);
    Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request);
    Task DeleteAsync(int id);
    Task<List<LookupItem>> GetLookupAsync(int? roleId = null);
    Task UpdateApplicationsAsync(int userId, List<int> applicationIds);
    Task GeneratePasswordResetTokenAsync(int userId);
    Task ResetPasswordAsync(ResetPasswordRequest request);
}
