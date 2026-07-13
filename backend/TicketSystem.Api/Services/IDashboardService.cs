using TicketSystem.Api.Models.DTOs.Tickets;

namespace TicketSystem.Api.Services;

public interface IDashboardService
{
    Task<TicketStatsResponse> GetStatsAsync(TicketStatsQueryParams query);
    Task<List<TicketSlaSummary>> GetSlaSummaryAsync();
    Task<List<AgentPerformanceResponse>> GetAgentPerformanceAsync();
    Task<TrendsResponse> GetTrendsAsync(int days = 30);
}
