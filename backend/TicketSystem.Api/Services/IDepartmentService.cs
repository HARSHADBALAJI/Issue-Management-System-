using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Departments;

namespace TicketSystem.Api.Services;

public interface IDepartmentService
{
    Task<PagedResponse<DepartmentResponse>> GetPagedAsync(DepartmentQueryParams query);
    Task<DepartmentResponse?> GetByIdAsync(int id);
    Task<DepartmentResponse> CreateAsync(CreateDepartmentRequest request);
    Task<DepartmentResponse> UpdateAsync(int id, UpdateDepartmentRequest request);
    Task DeleteAsync(int id);
    Task<List<LookupItem>> GetLookupAsync();
}
