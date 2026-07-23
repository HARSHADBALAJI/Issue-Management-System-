using System.ComponentModel.DataAnnotations;

namespace TicketSystem.Api.Models.Entities;

public class TicketAccessToken
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    [Required, MaxLength(255)]
    public string TokenHash { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    public bool IsRevoked { get; set; } = false;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    public int UsageCount { get; set; } = 0;

    public Ticket? Ticket { get; set; }
}
