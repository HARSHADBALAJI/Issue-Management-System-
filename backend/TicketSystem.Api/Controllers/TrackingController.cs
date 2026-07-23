using Microsoft.AspNetCore.Mvc;
using TicketSystem.Api.Services;

namespace TicketSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrackingController : ControllerBase
{
    private readonly ITrackingService _trackingService;
    private readonly IEmailService _emailService;
    private readonly ILogger<TrackingController> _logger;

    public TrackingController(ITrackingService trackingService, IEmailService emailService, ILogger<TrackingController> logger)
    {
        _trackingService = trackingService;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateToken([FromBody] GenerateTokenRequest request)
    {
        try
        {
            var token = await _trackingService.GenerateTokenAsync(request.TicketId, request.Email, request.ExpiryDays);
            return Ok(new { token, ticketId = request.TicketId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate tracking token");
            return StatusCode(500, new { error = "Failed to generate tracking token." });
        }
    }

    [HttpGet("validate/{ticketId}")]
    public async Task<IActionResult> ValidateToken(int ticketId, [FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Ok(new TrackingValidationResult { IsValid = false, ErrorMessage = "Token is required." });

        var result = await _trackingService.ValidateTokenAsync(ticketId, token);
        if (result == null)
            return Ok(new TrackingValidationResult { IsValid = false, ErrorMessage = "Invalid tracking link." });

        return Ok(result);
    }

    [HttpGet("my-tickets/{email}")]
    public async Task<IActionResult> GetMyTickets(string email, [FromQuery] int? ticketId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "Email is required." });

        var tickets = await _trackingService.GetMyTicketsAsync(email, ticketId);
        return Ok(new { tickets, email });
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeTokens([FromBody] RevokeTokenRequest request)
    {
        await _trackingService.RevokeTokensForTicketAsync(request.TicketId, request.Email);
        return Ok(new { message = "Tokens revoked." });
    }
}

public class GenerateTokenRequest
{
    public int TicketId { get; set; }
    public string Email { get; set; } = string.Empty;
    public int ExpiryDays { get; set; } = 30;
}

public class RevokeTokenRequest
{
    public int TicketId { get; set; }
    public string? Email { get; set; }
}
