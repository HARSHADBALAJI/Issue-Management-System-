using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Models.DTOs.Applications;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public class ApplicationRepository : Repository<Application>, IApplicationRepository
{
    public ApplicationRepository(TicketSystemDbContext context) : base(context) { }

    public override async Task<Application?> GetByIdAsync(int id)
        => await Context.Applications.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);

    public async Task<PagedResponse<ApplicationResponse>> GetPagedAsync(ApplicationQueryParams query)
    {
        var q = Context.Applications.AsNoTracking().IgnoreQueryFilters()
            .Include(a => a.Tickets)
            .Include(a => a.ApplicationAssignments)
                .ThenInclude(aa => aa.User)
                .ThenInclude(u => u.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(a => a.Name.Contains(query.Search));

        if (query.IsActive.HasValue)
            q = q.Where(a => a.IsActive == query.IsActive.Value);

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(a => a.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new ApplicationResponse
            {
                Id = a.Id,
                Name = a.Name,
                IsActive = a.IsActive,
                AssignedUserCount = a.ApplicationAssignments.Count(aa => aa.User.IsActive),
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                AssignedUsers = a.ApplicationAssignments.Where(aa => aa.User.IsActive).Select(aa => new ApplicationResponse.AssignedUserDto
                {
                    Id = aa.UserId,
                    FullName = aa.User.FullName,
                    RoleName = aa.User.Role.Name,
                    IsPrimarySPOC = aa.IsPrimarySPOC
                }).ToList()
            })
            .ToListAsync();

        return new PagedResponse<ApplicationResponse>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<ApplicationResponse?> GetApplicationResponseAsync(int id)
    {
        return await Context.Applications.AsNoTracking().IgnoreQueryFilters()
            .Include(a => a.Tickets)
            .Include(a => a.ApplicationAssignments)
                .ThenInclude(aa => aa.User)
                .ThenInclude(u => u.Role)
            .Where(a => a.Id == id)
            .Select(a => new ApplicationResponse
            {
                Id = a.Id,
                Name = a.Name,
                IsActive = a.IsActive,
                AssignedUserCount = a.ApplicationAssignments.Count(aa => aa.User.IsActive),
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                AssignedUsers = a.ApplicationAssignments.Where(aa => aa.User.IsActive).Select(aa => new ApplicationResponse.AssignedUserDto
                {
                    Id = aa.UserId,
                    FullName = aa.User.FullName,
                    RoleName = aa.User.Role.Name,
                    IsPrimarySPOC = aa.IsPrimarySPOC
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<LookupItem>> GetLookupAsync()
    {
        return await Context.Applications.AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new LookupItem
            {
                Id = a.Id,
                Name = a.Name
            })
            .ToListAsync();
    }

    public async Task UpdateUsersAsync(int applicationId, List<int> userIds, int? primarySpocUserId)
    {
        var existing = await Context.ApplicationAssignments
            .Where(aa => aa.ApplicationId == applicationId)
            .ToListAsync();

        var existingUserIds = existing.Select(aa => aa.UserId).ToHashSet();
        var incomingUserIds = userIds.ToHashSet();

        var toRemove = existing.Where(aa => !incomingUserIds.Contains(aa.UserId)).ToList();
        var toAdd = userIds.Where(id => !existingUserIds.Contains(id)).ToList();

        if (toRemove.Count > 0)
            Context.ApplicationAssignments.RemoveRange(toRemove);

        foreach (var userId in toAdd)
        {
            Context.ApplicationAssignments.Add(new ApplicationAssignment
            {
                UserId = userId,
                ApplicationId = applicationId,
                IsPrimarySPOC = userId == primarySpocUserId,
                AssignedAt = DateTime.UtcNow
            });
        }

        await Context.SaveChangesAsync();

        if (userIds.Count > 0)
        {
            var hasRule = await Context.ApplicationRoutingRules
                .AnyAsync(r => r.ApplicationId == applicationId);

            if (!hasRule)
            {
                var firstUser = await Context.Users
                    .Where(u => userIds.Contains(u.Id) && u.IsActive)
                    .OrderBy(u => u.Id)
                    .FirstOrDefaultAsync();

                if (firstUser != null)
                {
                    Context.ApplicationRoutingRules.Add(new ApplicationRoutingRule
                    {
                        ApplicationId = applicationId,
                        DepartmentId = firstUser.DepartmentId,
                        PrimarySpocUserId = firstUser.Id,
                        IsActive = true
                    });
                    await Context.SaveChangesAsync();
                }
            }
        }
    }

    public async Task<int> GetTicketCountAsync(int applicationId)
        => await Context.Tickets.CountAsync(t => t.ApplicationId == applicationId);

    public async Task HardDeleteAsync(int id)
    {
        var assignments = await Context.ApplicationAssignments
            .Where(aa => aa.ApplicationId == id).ToListAsync();
        Context.ApplicationAssignments.RemoveRange(assignments);

        var rules = await Context.ApplicationRoutingRules
            .Where(r => r.ApplicationId == id).ToListAsync();
        Context.ApplicationRoutingRules.RemoveRange(rules);

        var entity = await Context.Applications.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id);
        if (entity != null)
        {
            Context.Applications.Remove(entity);
            await Context.SaveChangesAsync();
        }
    }
}
