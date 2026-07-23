namespace TicketSystem.Api.Services;

public interface ITrackingService
{
    Task<string> GenerateTokenAsync(int ticketId, string email, int expiryDays = 30);
    Task<TrackingValidationResult?> ValidateTokenAsync(int ticketId, string token);
    Task<List<TrackingTicketSummary>> GetMyTicketsAsync(string email, int? currentTicketId = null);
    Task RevokeTokensForTicketAsync(int ticketId, string? email = null);
}

public class TrackingValidationResult
{
    public bool IsValid { get; set; }
    public int TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string StatusColor { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<TrackingMessage> Messages { get; set; } = new();
    public List<TrackingStatusHistory> StatusHistory { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class TrackingMessage
{
    public int Id { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TrackingAttachment> Attachments { get; set; } = new();
}

public class TrackingAttachment
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class TrackingStatusHistory
{
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string? ChangedBy { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TrackingTicketSummary
{
    public int TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string StatusColor { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
