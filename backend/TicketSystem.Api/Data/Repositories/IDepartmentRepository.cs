using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Departments;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public interface IDepartmentRepository : IRepository<Department>
{
    Task<PagedResponse<DepartmentResponse>> GetPagedAsync(DepartmentQueryParams query);
    Task<DepartmentResponse?> GetDepartmentResponseAsync(int id);
}
