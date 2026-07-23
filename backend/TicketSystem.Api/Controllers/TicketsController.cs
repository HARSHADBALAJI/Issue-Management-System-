using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Models.DTOs.Tickets;
using TicketSystem.Api.Services;

namespace TicketSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly TicketSystemDbContext _context;

    public TicketsController(ITicketService ticketService, TicketSystemDbContext context)
    {
        _ticketService = ticketService;
        _context = context;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsAdmin() =>
        User.IsInRole("Admin");

    private async Task<bool> HasTicketAccessAsync(int ticketId)
    {
        if (IsAdmin()) return true;
        var userId = GetUserId();
        return await _context.Tickets.AnyAsync(t =>
            t.Id == ticketId && (t.RequesterId == userId || t.AssignedToUserId == userId));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] TicketQueryParams query)
    {
        if (!IsAdmin())
            query.UserId = GetUserId();
        return Ok(await _ticketService.GetPagedAsync(query));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await HasTicketAccessAsync(id))
            return StatusCode(403, new { error = "You do not have access to this ticket." });
        var result = await _ticketService.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request)
        => Ok(await _ticketService.CreateAsync(request));

    [HttpPost("{id:int}/messages")]
    public async Task<IActionResult> AddMessage(int id)
    {
        if (!await HasTicketAccessAsync(id))
            return StatusCode(403, new { error = "You do not have access to this ticket." });

        CreateTicketMessageRequest request;
        List<IFormFile>? files = null;

        if (Request.ContentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            var form = await Request.ReadFormAsync();
            request = new CreateTicketMessageRequest
            {
                Content = form["content"]!,
                IsInternal = bool.TryParse(form["isInternal"], out var isInt) && isInt
            };
            files = form.Files.Count > 0 ? form.Files.ToList() : null;
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            request = JsonSerializer.Deserialize<CreateTicketMessageRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new CreateTicketMessageRequest();
        }

        return Ok(await _ticketService.AddMessageAsync(id, request, GetUserId(), files));
    }

    [HttpPost("{id:int}/corrective-actions")]
    public async Task<IActionResult> AddCorrectiveAction(int id, [FromBody] CreateCorrectiveActionRequest request)
    {
        if (!IsAdmin())
            return StatusCode(403, new { error = "Only admins can add corrective actions." });
        return Ok(await _ticketService.AddCorrectiveActionAsync(id, request, GetUserId()));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignTicketRequest request)
    {
        try
        {
            await _ticketService.AssignAsync(id, request, GetUserId());
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTicketStatusRequest request)
    {
        if (!await HasTicketAccessAsync(id))
            return StatusCode(403, new { error = "You do not have access to this ticket." });

        if (!IsAdmin())
        {
            var userId = GetUserId();
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();
            if (ticket.AssignedToUserId != userId && ticket.RequesterId != userId)
                return StatusCode(403, new { error = "You are not authorized to update this ticket's status." });
        }

        try
        {
            await _ticketService.UpdateStatusAsync(id, request, GetUserId());
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] TicketStatsQueryParams query)
    {
        if (!IsAdmin())
            query.UserId = GetUserId();
        return Ok(await _ticketService.GetStatsAsync(query));
    }

    [HttpGet("sla-summary")]
    public async Task<IActionResult> GetSlaSummary()
        => Ok(await _ticketService.GetSlaSummaryAsync(IsAdmin() ? null : GetUserId()));

    [Authorize(Roles = "Admin")]
    [HttpPost("bulk-assign")]
    public async Task<IActionResult> BulkAssign([FromBody] BulkAssignRequest request)
    {
        await _ticketService.BulkAssignAsync(request);
        return Ok(new { message = "Bulk assign completed" });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("bulk-status")]
    public async Task<IActionResult> BulkUpdateStatus([FromBody] BulkStatusRequest request)
        => Ok(await _ticketService.BulkUpdateStatusAsync(request));

    [Authorize(Roles = "Admin")]
    [AllowAnonymous]
    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> GetAttachment(int id, int attachmentId)
    {
        var attachment = await _context.TicketAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TicketMessage.TicketId == id);

        if (attachment == null) return NotFound();

        Response.Headers["Cache-Control"] = "private, max-age=3600";
        return File(attachment.FileData, attachment.ContentType, attachment.FileName);
    }

    [HttpPost("{id:int}/reopen")]
    public async Task<IActionResult> Reopen(int id)
    {
        if (!await HasTicketAccessAsync(id))
            return StatusCode(403, new { error = "You do not have access to this ticket." });

        try
        {
            await _ticketService.ReopenAsync(id, GetUserId());
            return Ok(new { message = "Ticket reopened" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
