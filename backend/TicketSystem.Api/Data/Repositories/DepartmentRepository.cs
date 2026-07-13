using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Departments;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public class DepartmentRepository : Repository<Department>, IDepartmentRepository
{
    public DepartmentRepository(TicketSystemDbContext context) : base(context) { }

    public async Task<PagedResponse<DepartmentResponse>> GetPagedAsync(DepartmentQueryParams query)
    {
        var q = Context.Departments.AsNoTracking()
            .Include(d => d.Users)
            .ThenInclude(u => u.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(d => d.Name.Contains(query.Search));

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(d => d.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(d => new DepartmentResponse
            {
                Id = d.Id,
                Name = d.Name,
                HeadUserId = d.Users.Where(u => u.IsActive).OrderBy(u => u.CreatedAt).Select(u => (int?)u.Id).FirstOrDefault(),
                HeadName = d.HeadName ?? d.Users.Where(u => u.IsActive).OrderBy(u => u.CreatedAt).Select(u => u.FullName).FirstOrDefault(),
                UserCount = d.Users.Count(u => u.IsActive),
                SpocCount = d.Users.Count(u => u.IsActive && u.Role.Name == "SPOC"),
                AdminCount = d.Users.Count(u => u.IsActive && u.Role.Name == "Admin"),
                IsActive = d.IsActive
            })
            .ToListAsync();

        return new PagedResponse<DepartmentResponse>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<DepartmentResponse?> GetDepartmentResponseAsync(int id)
    {
        return await Context.Departments.AsNoTracking()
            .Include(d => d.Users.Where(u => u.IsActive))
            .ThenInclude(u => u.Role)
            .Where(d => d.Id == id)
            .Select(d => new DepartmentResponse
            {
                Id = d.Id,
                Name = d.Name,
                HeadUserId = d.Users.OrderBy(u => u.CreatedAt).Select(u => (int?)u.Id).FirstOrDefault(),
                HeadName = d.HeadName ?? d.Users.OrderBy(u => u.CreatedAt).Select(u => u.FullName).FirstOrDefault(),
                UserCount = d.Users.Count(u => u.IsActive),
                SpocCount = d.Users.Count(u => u.IsActive && u.Role.Name == "SPOC"),
                AdminCount = d.Users.Count(u => u.IsActive && u.Role.Name == "Admin"),
                IsActive = d.IsActive
            })
            .FirstOrDefaultAsync();
    }
}
