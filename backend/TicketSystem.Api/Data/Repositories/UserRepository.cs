using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Users;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(TicketSystemDbContext context) : base(context) { }

    public async Task<PagedResponse<UserResponse>> GetPagedAsync(UserQueryParams query)
    {
        var q = Context.Users.AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.Role)
            .Include(u => u.ApplicationAssignments)
            .ThenInclude(aa => aa.Application)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(u => u.FullName.Contains(query.Search) || u.Email.Contains(query.Search));

        if (query.RoleId.HasValue)
            q = q.Where(u => u.RoleId == query.RoleId.Value);

        if (query.DepartmentId.HasValue)
            q = q.Where(u => u.DepartmentId == query.DepartmentId.Value);

        if (query.IsActive.HasValue)
            q = q.Where(u => u.IsActive == query.IsActive.Value);

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(u => u.FullName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Name = u.FullName,
                Email = u.Email,
                DepartmentId = u.DepartmentId,
                DepartmentName = u.Department.Name,
                RoleId = u.RoleId,
                Role = u.Role.Name.ToLower(),
                Status = u.IsActive ? "active" : "inactive",
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt,
                ApplicationIds = u.ApplicationAssignments.Select(aa => aa.ApplicationId).ToList(),
                AssignedApps = u.ApplicationAssignments.Select(aa => aa.Application.Name).ToList()
            })
            .ToListAsync();

        return new PagedResponse<UserResponse>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<UserResponse?> GetUserResponseAsync(int id)
    {
        return await Context.Users.AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.Role)
            .Include(u => u.ApplicationAssignments)
            .ThenInclude(aa => aa.Application)
            .Where(u => u.Id == id)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Name = u.FullName,
                Email = u.Email,
                DepartmentId = u.DepartmentId,
                DepartmentName = u.Department.Name,
                RoleId = u.RoleId,
                Role = u.Role.Name.ToLower(),
                Status = u.IsActive ? "active" : "inactive",
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt,
                ApplicationIds = u.ApplicationAssignments.Select(aa => aa.ApplicationId).ToList(),
                AssignedApps = u.ApplicationAssignments.Select(aa => aa.Application.Name).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
        => await Context.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<List<LookupItem>> GetLookupAsync(int? roleId = null)
    {
        var q = Context.Users.AsNoTracking()
            .Include(u => u.Department)
            .Where(u => u.IsActive);

        if (roleId.HasValue)
            q = q.Where(u => u.RoleId == roleId.Value);

        return await q
            .OrderBy(u => u.FullName)
            .Select(u => new LookupItem
            {
                Id = u.Id,
                Name = u.FullName,
                AdditionalInfo = u.Department.Name
            })
            .ToListAsync();
    }

    public async Task<List<int>> GetApplicationIdsAsync(int userId)
    {
        return await Context.ApplicationAssignments
            .Where(aa => aa.UserId == userId)
            .Select(aa => aa.ApplicationId)
            .ToListAsync();
    }

    public async Task UpdateRoutingRulesForUserAsync(int userId, int oldDepartmentId, int newDepartmentId)
    {
        var rules = await Context.ApplicationRoutingRules
            .Where(r => r.PrimarySpocUserId == userId && r.DepartmentId == oldDepartmentId)
            .ToListAsync();

        if (rules.Count == 0) return;

        var appIds = rules.Select(r => r.ApplicationId).ToHashSet();
        Context.ApplicationRoutingRules.RemoveRange(rules);

        foreach (var appId in appIds)
        {
            var hasRule = await Context.ApplicationRoutingRules
                .AnyAsync(r => r.ApplicationId == appId && r.DepartmentId == newDepartmentId);
            if (!hasRule)
            {
                Context.ApplicationRoutingRules.Add(new ApplicationRoutingRule
                {
                    ApplicationId = appId,
                    DepartmentId = newDepartmentId,
                    PrimarySpocUserId = userId,
                    IsActive = true
                });
            }
        }

        await Context.SaveChangesAsync();
    }

    public async Task UpdateApplicationsAsync(int userId, List<int> applicationIds)
    {
        var existing = await Context.ApplicationAssignments
            .Where(aa => aa.UserId == userId)
            .ToListAsync();

        var existingAppIds = existing.Select(aa => aa.ApplicationId).ToHashSet();
        var incomingAppIds = applicationIds.ToHashSet();

        var toRemove = existing.Where(aa => !incomingAppIds.Contains(aa.ApplicationId)).ToList();
        var toAdd = applicationIds.Where(id => !existingAppIds.Contains(id)).ToList();

        var preservedSpoc = existing
            .Where(aa => aa.IsPrimarySPOC)
            .ToDictionary(aa => aa.ApplicationId, aa => aa.IsPrimarySPOC);

        if (toRemove.Count > 0)
            Context.ApplicationAssignments.RemoveRange(toRemove);

        foreach (var appId in toAdd)
        {
            Context.ApplicationAssignments.Add(new ApplicationAssignment
            {
                UserId = userId,
                ApplicationId = appId,
                IsPrimarySPOC = preservedSpoc.ContainsKey(appId),
                AssignedAt = DateTime.UtcNow
            });
        }

        await Context.SaveChangesAsync();

        // auto-create routing rules for newly assigned apps that have none
        if (toAdd.Count > 0)
        {
            var user = await Context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null && user.IsActive)
            {
                foreach (var appId in toAdd)
                {
                    var hasRule = await Context.ApplicationRoutingRules
                        .AnyAsync(r => r.ApplicationId == appId);
                    if (!hasRule)
                    {
                        Context.ApplicationRoutingRules.Add(new ApplicationRoutingRule
                        {
                            ApplicationId = appId,
                            DepartmentId = user.DepartmentId,
                            PrimarySpocUserId = user.Id,
                            IsActive = true
                        });
                    }
                }
                await Context.SaveChangesAsync();
            }
        }
    }
}
