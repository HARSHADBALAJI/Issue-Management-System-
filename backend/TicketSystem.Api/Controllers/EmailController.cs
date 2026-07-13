using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Api.Services;

namespace TicketSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ITicketService _ticketService;
    private readonly ILogger<EmailController> _logger;

    public EmailController(IEmailService emailService, ITicketService ticketService, ILogger<EmailController> logger)
    {
        _emailService = emailService;
        _ticketService = ticketService;
        _logger = logger;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    /// <summary>
    /// Send an email reply from SPOC to requester when replying from frontend.
    /// This is called AFTER the message has been saved to DB via POST /api/tickets/{id}/messages.
    /// </summary>
    [HttpPost("send-reply/{ticketId}")]
    public async Task<IActionResult> SendReply(int ticketId, [FromBody] SendReplyRequest request)
    {
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
