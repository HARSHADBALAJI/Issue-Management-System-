using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Models.DTOs.Tickets;

namespace TicketSystem.Api.Services;

public class DashboardService : IDashboardService
{
    private readonly ITicketService _ticketService;
    private readonly TicketSystemDbContext _context;

    public DashboardService(ITicketService ticketService, TicketSystemDbContext context)
    {
        _ticketService = ticketService;
        _context = context;
    }

    public async Task<TicketStatsResponse> GetStatsAsync(TicketStatsQueryParams query)
        => await _ticketService.GetStatsAsync(query);

    public async Task<List<TicketSlaSummary>> GetSlaSummaryAsync(int? userId = null)
        => await _ticketService.GetSlaSummaryAsync(userId);

    public async Task<List<AgentPerformanceResponse>> GetAgentPerformanceAsync()
    {
        var now = DateTime.UtcNow;
        var agentData = await _context.Users
            .Include(u => u.AssignedTickets)
                .ThenInclude(t => t.Status)
            .Include(u => u.AssignedTickets)
                .ThenInclude(t => t.TicketSlas)
            .Where(u => u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                Tickets = u.AssignedTickets
            })
            .ToListAsync();

        var result = agentData.Select(u =>
        {
            var total = u.Tickets.Count;
            if (total == 0) return null;
            var resolved = u.Tickets.Count(t => t.Status.Name == "resolved" || t.Status.Name == "closed");
            var open = u.Tickets.Count(t => t.Status.Name == "in_progress" || t.Status.Name == "waiting");
            var slaOk = u.Tickets.Count(t =>
                !t.TicketSlas.Any(s => s.IsActive) ||
                t.TicketSlas.Any(s => s.IsActive && s.Status != Models.Entities.SlaStatus.Breached));
            var slaPct = total > 0 ? Math.Round((double)slaOk / total * 100, 1) : 0;
            var resolvedHours = u.Tickets.Where(t => t.ResolvedAt != null)
                .Select(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalHours).ToList();
            var avgHours = resolvedHours.Count > 0 ? resolvedHours.Average() : 0;

            return new AgentPerformanceResponse
            {
                AgentId = u.Id,
                AgentName = u.FullName,
                Assigned = total,
                Resolved = resolved,
                Open = open,
                SlaPercentage = slaPct,
                AvgResolutionTime = $"{avgHours:F1}h"
            };
        })
        .Where(a => a != null)
        .ToList()!;

        return result;
    }

    public async Task<TrendsResponse> GetTrendsAsync(int days = 30, int? userId = null)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);

        var q = _context.Tickets
            .Include(t => t.Status)
            .Where(t => t.CreatedAt >= startDate)
            .AsQueryable();

        if (userId.HasValue)
            q = q.Where(t => t.RequesterId == userId.Value || t.AssignedToUserId == userId.Value);

        var tickets = await q.ToListAsync();

        var labels = new List<string>();
        var created = new List<int>();
        var resolved = new List<int>();

        for (int i = days - 1; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddDays(-i).Date;
            labels.Add(date.ToString("MMM dd"));
            created.Add(tickets.Count(t => t.CreatedAt.Date == date));
            resolved.Add(tickets.Count(t => t.ResolvedAt != null && t.ResolvedAt.Value.Date == date));
        }

        return new TrendsResponse
        {
            Labels = labels,
            Series = new List<TrendsResponse.SeriesData>
            {
                new() { Key = "created", Label = "Created", Data = created },
                new() { Key = "resolved", Label = "Resolved", Data = resolved }
            }
        };
    }
}
