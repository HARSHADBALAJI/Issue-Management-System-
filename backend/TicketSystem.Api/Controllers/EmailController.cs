using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Services;

namespace TicketSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ITicketService _ticketService;
    private readonly TicketSystemDbContext _context;
    private readonly ILogger<EmailController> _logger;

    public EmailController(IEmailService emailService, ITicketService ticketService, TicketSystemDbContext context, ILogger<EmailController> logger)
    {
        _emailService = emailService;
        _ticketService = ticketService;
        _context = context;
        _logger = logger;
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

    [HttpPost("send-reply/{ticketId}")]
    public async Task<IActionResult> SendReply(int ticketId, [FromBody] SendReplyRequest request)
    {
        if (!await HasTicketAccessAsync(ticketId))
            return StatusCode(403, new { error = "You do not have access to this ticket." });

        var result = await _emailService.SendReplyAsync(
            request.ToEmail,
            request.Subject,
            request.Body,
            request.InReplyTo,
            request.References
        );

        if (!result.success)
        {
            _logger.LogWarning("Failed to send email reply for ticket {TicketId}: {Error}", ticketId, result.error);
            return Ok(new { sent = false, error = result.error });
        }

        return Ok(new { sent = true });
    }
}

public class SendReplyRequest
{
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? InReplyTo { get; set; }
    public string? References { get; set; }
}
