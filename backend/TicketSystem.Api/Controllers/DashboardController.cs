using System.Security.Claims;
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

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsAdmin() =>
        User.IsInRole("Admin");

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] TicketStatsQueryParams query)
    {
        if (!IsAdmin())
            query.UserId = GetUserId();
        return Ok(await _dashboardService.GetStatsAsync(query));
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends([FromQuery] int days = 30)
        => Ok(await _dashboardService.GetTrendsAsync(days, IsAdmin() ? null : GetUserId()));

    [HttpGet("sla")]
    public async Task<IActionResult> GetSlaSummary()
        => Ok(await _dashboardService.GetSlaSummaryAsync(IsAdmin() ? null : GetUserId()));

    [Authorize(Roles = "Admin")]
    [HttpGet("agent-performance")]
    public async Task<IActionResult> GetAgentPerformance()
        => Ok(await _dashboardService.GetAgentPerformanceAsync());
}
