using Microsoft.AspNetCore.Http;

namespace TicketSystem.Api.Models.DTOs.Tickets;

public class CreateTicketMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public List<IFormFile>? Files { get; set; }
}
