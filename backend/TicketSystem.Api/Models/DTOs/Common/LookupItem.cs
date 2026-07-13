namespace TicketSystem.Api.Models.DTOs.Common;

public class LookupItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AdditionalInfo { get; set; }
}
