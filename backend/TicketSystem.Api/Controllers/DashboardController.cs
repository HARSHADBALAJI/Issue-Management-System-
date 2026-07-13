using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Api.Models.DTOs.Tickets;
using TicketSystem.Api.Services;

namespace TicketSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService) => _dashboardService = dashboardService;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] TicketStatsQueryParams query)
        => Ok(await _dashboardService.GetStatsAsync(query));

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends([FromQuery] int days = 30)
        => Ok(await _dashboardService.GetTrendsAsync(days));

    [HttpGet("sla")]
    public async Task<IActionResult> GetSlaSummary()
        => Ok(await _dashboardService.GetSlaSummaryAsync());

    [HttpGet("agent-performance")]
    public async Task<IActionResult> GetAgentPerformance()
        => Ok(await _dashboardService.GetAgentPerformanceAsync());
}
