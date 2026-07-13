using Microsoft.AspNetCore.Http;

namespace TicketSystem.Api.Models.DTOs.Tickets;

public class CreateTicketRequest
{
    public int RequesterId { get; set; }
    public int ApplicationId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public List<IFormFile>? Files { get; set; }
}
