using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Tickets;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public class TicketRepository : Repository<Ticket>, ITicketRepository
{
    public TicketRepository(TicketSystemDbContext context) : base(context) { }

    public async Task<Ticket?> GetByIdWithIncludesAsync(int id)
    {
        return await Context.Tickets
            .Include(t => t.Requester)
            .Include(t => t.Application)
            .Include(t => t.Status)
            .Include(t => t.AssignedToUser)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<PagedResponse<TicketListResponse>> GetPagedAsync(TicketQueryParams query)
    {
        var q = Context.Tickets.AsNoTracking()
            .Include(t => t.Requester)
            .Include(t => t.Application)
            .Include(t => t.Status)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Messages)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(t => t.TicketNumber.Contains(query.Search) || t.Subject.Contains(query.Search));

        if (query.ApplicationId.HasValue)
            q = q.Where(t => t.ApplicationId == query.ApplicationId.Value);

        if (query.StatusId.HasValue)
            q = q.Where(t => t.StatusId == query.StatusId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(t => t.Status.Name == query.Status);

        if (query.AssignedTo.HasValue)
            q = q.Where(t => t.AssignedToUserId == query.AssignedTo.Value);

        if (query.Unassigned == true)
            q = q.Where(t => t.AssignedToUserId == null);

        if (!string.IsNullOrWhiteSpace(query.Priority))
            q = q.Where(t => t.Priority == query.Priority);

        if (query.RequesterId.HasValue)
            q = q.Where(t => t.RequesterId == query.RequesterId.Value);

        if (query.SlaBreached == true)
            q = q.Where(t => t.TicketSlas.Any(s => s.IsActive && s.Status == SlaStatus.Breached));

        if (query.UserId.HasValue)
            q = q.Where(t => t.RequesterId == query.UserId.Value || t.AssignedToUserId == query.UserId.Value);

        var total = await q.CountAsync();

        var sortDir = string.IsNullOrWhiteSpace(query.SortDir) || query.SortDir == "desc";

        var ordered = query.SortBy switch
        {
            "ticketNumber" => sortDir ? q.OrderByDescending(t => t.TicketNumber) : q.OrderBy(t => t.TicketNumber),
            "subject" => sortDir ? q.OrderByDescending(t => t.Subject) : q.OrderBy(t => t.Subject),
            "status" => sortDir ? q.OrderByDescending(t => t.Status.Name) : q.OrderBy(t => t.Status.Name),
            "priority" => sortDir ? q.OrderByDescending(t => t.Priority) : q.OrderBy(t => t.Priority),
            "assignedTo" => sortDir ? q.OrderByDescending(t => t.AssignedToUser!.FullName) : q.OrderBy(t => t.AssignedToUser!.FullName),
            _ => sortDir ? q.OrderByDescending(t => t.UpdatedAt) : q.OrderBy(t => t.UpdatedAt)
        };

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new TicketListResponse
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Subject = t.Subject,
                Description = t.Description,
                StatusId = t.StatusId,
                StatusName = t.Status.Name,
                StatusDisplayName = t.Status.DisplayName,
                Priority = t.Priority,
                RequesterId = t.RequesterId,
                RequesterName = t.Requester.FullName,
                ApplicationId = t.ApplicationId,
                ApplicationName = t.Application.Name,
                AssignedToId = t.AssignedToUserId,
                AssignedToName = t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
                LastMessageAt = t.Messages.OrderByDescending(m => m.CreatedAt).Select(m => (DateTime?)m.CreatedAt).FirstOrDefault(),
                SlaDeadline = t.TicketSlas
                    .Where(s => s.IsActive)
                    .Select(s => (DateTime?)s.DeadlineAt)
                    .FirstOrDefault(),
                IsSlaBreached = t.TicketSlas
                    .Any(s => s.IsActive && s.Status == SlaStatus.Breached),
                SlaStatus = t.TicketSlas
                    .Where(s => s.IsActive)
                    .Select(s => s.Status.ToString())
                    .FirstOrDefault() ?? string.Empty,
                SlaRemainingPercent = t.TicketSlas
                    .Where(s => s.IsActive)
                    .Select(s => s.Status == SlaStatus.Completed ? 100 :
                        s.Status == SlaStatus.Breached ? 0 :
                        (s.DeadlineAt > DateTime.UtcNow
                            ? (int)Math.Round(Math.Min((DateTime.UtcNow - s.StartedAt.GetValueOrDefault(s.CreatedAt)).TotalMinutes / (s.DeadlineAt - s.StartedAt.GetValueOrDefault(s.CreatedAt)).TotalMinutes * 100, 100), 0)
                            : 0))
                    .FirstOrDefault(),
                SlaRemainingTime = t.TicketSlas
                    .Where(s => s.IsActive)
                    .Select(s => s.Status == SlaStatus.Breached ? "Breached" :
                        s.Status == SlaStatus.Completed ? "Completed" :
                        s.DeadlineAt > DateTime.UtcNow
                            ? ((s.DeadlineAt - DateTime.UtcNow).TotalDays >= 1
                                ? $"{(int)(s.DeadlineAt - DateTime.UtcNow).TotalDays}d {((s.DeadlineAt - DateTime.UtcNow).Hours)}h"
                                : $"{(int)(s.DeadlineAt - DateTime.UtcNow).TotalHours}h {((s.DeadlineAt - DateTime.UtcNow).Minutes)}m")
                            : "Overdue")
                    .FirstOrDefault() ?? string.Empty,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        return new PagedResponse<TicketListResponse>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<TicketDetailResponse?> GetDetailAsync(int id)
    {
        var ticket = await Context.Tickets.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.Subject,
                t.Description,
                t.StatusId,
                StatusName = t.Status.Name,
                StatusDisplayName = t.Status.DisplayName,
                StatusColor = t.Status.Color,
                t.Priority,
                t.RequesterId,
                RequesterName = t.Requester.FullName,
                RequesterEmail = t.Requester.Email,
                t.ApplicationId,
                ApplicationName = t.Application.Name,
                t.AssignedToUserId,
                AssignedToName = t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
                AssignedToDepartment = t.AssignedToUser != null ? t.AssignedToUser.Department.Name : null,
                t.SlaDeadline,
                t.SlaBreachedAt,
                ResolutionAt = t.ResolvedAt,
                ClosedAt = t.ClosedAt,
                t.CreatedAt,
                t.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (ticket == null) return null;

        var lastIds = await Context.TicketMessages.AsNoTracking()
            .Where(m => m.TicketId == id)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.Id)
            .Take(50)
            .ToListAsync();

        var messages = await Context.TicketMessages.AsNoTracking()
            .Where(m => lastIds.Contains(m.Id))
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TicketDetailResponse.MessageDto
            {
                Id = m.Id,
                Content = m.Content,
                IsInternal = m.IsInternal,
                IsFromRequester = m.RequesterId != null,
                CreatedById = m.UserId ?? 0,
                CreatedByName = m.User != null ? m.User.FullName : (m.Requester != null ? m.Requester.FullName : "System"),
                CreatedByEmail = m.User != null ? m.User.Email : (m.Requester != null ? m.Requester.Email : null),
                MessageSourceType = m.MessageSourceType,
                CreatedAt = m.CreatedAt,
                Attachments = m.Attachments.Select(a => new TicketDetailResponse.AttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize
                }).ToList()
            })
            .ToListAsync();

        var statusHistory = await Context.TicketStatusHistories.AsNoTracking()
            .Where(h => h.TicketId == id)
            .OrderBy(h => h.CreatedAt)
            .Select(h => new TicketDetailResponse.StatusHistoryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus != null ? h.FromStatus.Name : null,
                ToStatus = h.ToStatus.Name,
                ChangedById = h.ChangedByUserId ?? 0,
                ChangedByName = h.ChangedByUser != null ? h.ChangedByUser.FullName : (h.ChangedByRequester != null ? h.ChangedByRequester.FullName : "System"),
                Note = h.Remarks,
                CreatedAt = h.CreatedAt
            })
            .ToListAsync();

        var correctiveActions = await Context.TicketCorrectiveActions.AsNoTracking()
            .Where(ca => ca.TicketId == id)
            .OrderBy(ca => ca.CreatedAt)
            .Select(ca => new TicketDetailResponse.CorrectiveActionDto
            {
                Id = ca.Id,
                Description = ca.Description,
                CreatedByName = ca.PerformedByUser.FullName,
                CreatedAt = ca.CreatedAt
            })
            .ToListAsync();

        var appAlias = ticket.ApplicationName.Length > 10
            ? ticket.ApplicationName.Substring(0, 10).ToUpper().Replace(" ", "")
            : ticket.ApplicationName.ToUpper().Replace(" ", "");

        return new TicketDetailResponse
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Subject = ticket.Subject,
            Description = ticket.Description,
            StatusId = ticket.StatusId,
            StatusName = ticket.StatusName,
            StatusDisplayName = ticket.StatusDisplayName,
            StatusColor = ticket.StatusColor,
            Priority = ticket.Priority,
            RequesterId = ticket.RequesterId,
            RequesterName = ticket.RequesterName,
            RequesterEmail = ticket.RequesterEmail,
            ApplicationId = ticket.ApplicationId,
            ApplicationName = ticket.ApplicationName,
            ApplicationAlias = appAlias,
            AssignedToId = ticket.AssignedToUserId,
            AssignedToName = ticket.AssignedToName,
            AssignedToDepartment = ticket.AssignedToDepartment,
            SlaDeadline = ticket.SlaDeadline,
            SlaBreachedAt = ticket.SlaBreachedAt,
            ResolutionAt = ticket.ResolutionAt,
            ClosedAt = ticket.ClosedAt,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            Messages = messages,
            StatusHistory = statusHistory,
            CorrectiveActions = correctiveActions
        };
    }

    public async Task<TicketListResponse?> GetTicketListResponseAsync(int id)
    {
        return await Context.Tickets.AsNoTracking()
            .Include(t => t.Requester)
            .Include(t => t.Application)
            .Include(t => t.Status)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Messages)
            .Where(t => t.Id == id)
            .Select(t => new TicketListResponse
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Subject = t.Subject,
                Description = t.Description,
                StatusId = t.StatusId,
                StatusName = t.Status.Name,
                StatusDisplayName = t.Status.DisplayName,
                Priority = t.Priority,
                RequesterId = t.RequesterId,
                RequesterName = t.Requester.FullName,
                ApplicationId = t.ApplicationId,
                ApplicationName = t.Application.Name,
                AssignedToId = t.AssignedToUserId,
                AssignedToName = t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
                LastMessageAt = t.Messages.OrderByDescending(m => m.CreatedAt).Select(m => (DateTime?)m.CreatedAt).FirstOrDefault(),
                SlaDeadline = t.TicketSlas
                    .Where(s => s.IsActive)
                    .Select(s => (DateTime?)s.DeadlineAt)
                    .FirstOrDefault(),
                IsSlaBreached = t.TicketSlas
                    .Any(s => s.IsActive && s.Status == SlaStatus.Breached),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetNextSequenceAsync()
    {
        var maxTicketNumber = await Context.Tickets
            .Where(t => t.TicketNumber.StartsWith("TKT-"))
            .Select(t => t.TicketNumber)
            .ToListAsync();

        var maxNum = 0;
        foreach (var tn in maxTicketNumber)
        {
            if (int.TryParse(tn.AsSpan(4), out var num) && num > maxNum)
                maxNum = num;
        }

        return maxNum + 1;
    }

    public async Task<TicketStatsResponse> GetStatsAsync(TicketStatsQueryParams query)
    {
        var q = Context.Tickets.AsNoTracking()
            .Include(t => t.Status)
            .AsQueryable();

        if (query.StartDate.HasValue)
            q = q.Where(t => t.CreatedAt >= query.StartDate.Value);
        if (query.EndDate.HasValue)
            q = q.Where(t => t.CreatedAt <= query.EndDate.Value);
        if (query.ApplicationId.HasValue)
            q = q.Where(t => t.ApplicationId == query.ApplicationId.Value);
        if (query.UserId.HasValue)
            q = q.Where(t => t.RequesterId == query.UserId.Value || t.AssignedToUserId == query.UserId.Value);

        var total = await q.CountAsync();
        var open = await q.CountAsync(t => t.Status.Name == "open");
        var inProgress = await q.CountAsync(t => t.Status.Name == "in_progress");
        var waiting = await q.CountAsync(t => t.Status.Name == "waiting");
        var resolved = await q.CountAsync(t => t.Status.Name == "resolved");
        var closed = await q.CountAsync(t => t.Status.Name == "closed");

        var ticketIds = await q.Select(t => t.Id).ToListAsync();

        var breached = ticketIds.Count > 0
            ? await Context.TicketSlas.AsNoTracking()
                .CountAsync(ts => ticketIds.Contains(ts.TicketId) && ts.IsActive && ts.Status == SlaStatus.Breached)
            : 0;

        var resolvedTickets = await q.Where(t => t.ResolvedAt != null)
            .Select(t => new { t.ResolvedAt, t.CreatedAt })
            .ToListAsync();
        var avgResolution = resolvedTickets.Count > 0
            ? resolvedTickets.Average(x => (x.ResolvedAt!.Value - x.CreatedAt).TotalHours)
            : 0;

        var totalWithSla = ticketIds.Count > 0
            ? await Context.TicketSlas.AsNoTracking()
                .CountAsync(ts => ticketIds.Contains(ts.TicketId) && ts.IsActive)
            : 0;
        var slaCompliance = totalWithSla > 0
            ? Math.Round((double)(totalWithSla - breached) / totalWithSla * 100, 1)
            : 0;

        var priorityGroups = await q
            .GroupBy(t => t.Priority)
            .Select(g => new TicketStatsResponse.PriorityDistItem
            {
                Priority = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        return new TicketStatsResponse
        {
            Total = total,
            Open = inProgress + waiting + resolved,
            InProgress = inProgress,
            Waiting = waiting,
            Resolved = resolved,
            Closed = closed,
            SlaBreached = breached,
            AvgResolutionTime = $"{avgResolution:F1}h",
            SlaCompliance = slaCompliance,
            PriorityDistribution = priorityGroups
        };
    }

    public async Task<List<TicketSlaSummary>> GetSlaSummaryAsync(int? userId = null)
    {
        var now = DateTime.UtcNow;
        var tickets = await Context.Tickets.AsNoTracking()
            .Include(t => t.Status)
            .Include(t => t.AssignedToUser)
            .Include(t => t.TicketSlas)
            .Where(t => t.Status.Name != "closed")
            .Where(t => !userId.HasValue || t.RequesterId == userId.Value || t.AssignedToUserId == userId.Value)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return tickets.AsEnumerable()
            .Select(t =>
            {
                var activeSla = t.TicketSlas.FirstOrDefault(s => s.IsActive);
                var slaDeadline = activeSla?.DeadlineAt ?? t.SlaDeadline;
                double percentage = 0;
                if (activeSla != null)
                {
                    if (activeSla.Status == SlaStatus.Completed)
                        percentage = 100;
                    else if (activeSla.Status == SlaStatus.Breached)
                        percentage = 100;
                    else
                    {
                        var elapsed = (now - activeSla.StartedAt.GetValueOrDefault(now)).TotalMinutes - activeSla.TotalPausedDuration.TotalMinutes;
                        var total = (activeSla.DeadlineAt - activeSla.StartedAt.GetValueOrDefault(activeSla.CreatedAt)).TotalMinutes;
                        percentage = total > 0 ? Math.Round(Math.Min(elapsed / total * 100, 100), 1) : 0;
                    }
                }
                string FormatTime(TimeSpan ts)
                {
                    if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
                    if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
                    return $"{(int)ts.TotalMinutes}m";
                }

                string? timeRemaining = null;
                if (activeSla != null && slaDeadline.HasValue)
                {
                    var remaining = activeSla.Status == SlaStatus.Breached
                        ? TimeSpan.Zero
                        : slaDeadline.Value - now;
                    if (remaining.TotalSeconds <= 0)
                        timeRemaining = "Breached";
                    else
                        timeRemaining = FormatTime(remaining);
                }

                return new TicketSlaSummary
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber,
                    Subject = t.Subject,
                    StatusName = t.Status.Name,
                    Priority = t.Priority,
                    AssignedToName = t.AssignedToUser?.FullName,
                    SlaDeadline = slaDeadline,
                    Percentage = percentage,
                    SlaStatus = activeSla?.Status.ToString() ?? "None",
                    TimeRemaining = timeRemaining
                };
            })
            .ToList();
    }

    public async Task<TicketMessage> AddMessageAsync(TicketMessage message)
    {
        await Context.TicketMessages.AddAsync(message);
        await Context.SaveChangesAsync();
        return message;
    }

    public async Task<TicketStatusHistory> AddStatusHistoryAsync(TicketStatusHistory history)
    {
        await Context.TicketStatusHistories.AddAsync(history);
        await Context.SaveChangesAsync();
        return history;
    }

    public async Task<TicketCorrectiveAction> AddCorrectiveActionAsync(TicketCorrectiveAction action)
    {
        await Context.TicketCorrectiveActions.AddAsync(action);
        await Context.SaveChangesAsync();
        return action;
    }

    public async Task BulkAssignAsync(List<int> ticketIds, int assignedToUserId)
    {
        await Context.Tickets
            .Where(t => ticketIds.Contains(t.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.AssignedToUserId, assignedToUserId)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));
    }

    public async Task BulkUpdateStatusAsync(List<int> ticketIds, int statusId)
    {
        await Context.Tickets
            .Where(t => ticketIds.Contains(t.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.StatusId, statusId)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));
    }
}
