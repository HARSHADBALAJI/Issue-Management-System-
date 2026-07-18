using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Api.Models.DTOs.Sla;
using TicketSystem.Api.Services;

namespace TicketSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sla")]
public class SlaController : ControllerBase
{
    private readonly ISlaService _slaService;
    private readonly ILogger<SlaController> _logger;

    public SlaController(ISlaService slaService, ILogger<SlaController> logger)
    {
        _slaService = slaService;
        _logger = logger;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            var settings = await _slaService.GetSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SLA settings");
            return StatusCode(500, "An error occurred while fetching SLA settings");
        }
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSlaSettingsRequest request)
    {
        try
        {
            await _slaService.UpdateSettingsAsync(request);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update SLA settings");
            return StatusCode(500, "An error occurred while updating SLA settings");
        }
    }

    [HttpGet("holidays")]
    public async Task<IActionResult> GetHolidays()
    {
        try
        {
            var holidays = await _slaService.GetHolidaysAsync();
            return Ok(holidays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get holidays");
            return StatusCode(500, "An error occurred while fetching holidays");
        }
    }

    [HttpPost("holidays")]
    public async Task<IActionResult> CreateHoliday([FromBody] CreateHolidayRequest request)
    {
        try
        {
            var holiday = await _slaService.CreateHolidayAsync(request);
            return Ok(holiday);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create holiday");
            return StatusCode(500, "An error occurred while creating holiday");
        }
    }

    [HttpPut("holidays/{id:int}")]
    public async Task<IActionResult> UpdateHoliday(int id, [FromBody] CreateHolidayRequest request)
    {
        try
        {
            await _slaService.UpdateHolidayAsync(id, request);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Holiday not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update holiday {Id}", id);
            return StatusCode(500, "An error occurred while updating holiday");
        }
    }

    [HttpDelete("holidays/{id:int}")]
    public async Task<IActionResult> DeleteHoliday(int id)
    {
        try
        {
            await _slaService.DeleteHolidayAsync(id);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Holiday not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete holiday {Id}", id);
            return StatusCode(500, "An error occurred while deleting holiday");
        }
    }

    [HttpGet("weekly-rules")]
    public async Task<IActionResult> GetWeeklyRules()
    {
        try
        {
            var rules = await _slaService.GetWeeklyRulesAsync();
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get weekly rules");
            return StatusCode(500, "An error occurred while fetching weekly rules");
        }
    }

    [HttpPost("weekly-rules")]
    public async Task<IActionResult> CreateWeeklyRule([FromBody] CreateWeeklyHolidayRuleRequest request)
    {
        try
        {
            var rule = await _slaService.CreateWeeklyRuleAsync(request);
            return Ok(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create weekly rule");
            return StatusCode(500, "An error occurred while creating weekly rule");
        }
    }

    [HttpDelete("weekly-rules/{id:int}")]
    public async Task<IActionResult> DeleteWeeklyRule(int id)
    {
        try
        {
            await _slaService.DeleteWeeklyRuleAsync(id);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Weekly rule not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete weekly rule {Id}", id);
            return StatusCode(500, "An error occurred while deleting weekly rule");
        }
    }

    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicies()
    {
        try
        {
            var policies = await _slaService.GetPoliciesAsync();
            return Ok(policies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SLA policies");
            return StatusCode(500, "An error occurred while fetching SLA policies");
        }
    }

    [HttpPut("policies/{id:int}")]
    public async Task<IActionResult> UpdatePolicy(int id, [FromBody] UpdateSlaPolicyRequest request)
    {
        try
        {
            await _slaService.UpdatePolicyAsync(id, request);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("SLA policy not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update SLA policy {Id}", id);
            return StatusCode(500, "An error occurred while updating SLA policy");
        }
    }

    [HttpGet("tickets/{ticketId:int}")]
    public async Task<IActionResult> GetTicketSla(int ticketId)
    {
        try
        {
            var sla = await _slaService.GetTicketSlaAsync(ticketId);
            if (sla == null) return Ok(new { });
            return Ok(sla);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SLA for ticket {TicketId}", ticketId);
            return StatusCode(500, "An error occurred while fetching ticket SLA");
        }
    }

    [HttpGet("tickets/{ticketId:int}/audit")]
    public async Task<IActionResult> GetSlaAudit(int ticketId)
    {
        try
        {
            var audit = await _slaService.GetSlaAuditAsync(ticketId);
            return Ok(audit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SLA audit for ticket {TicketId}", ticketId);
            return StatusCode(500, "An error occurred while fetching SLA audit");
        }
    }
}
