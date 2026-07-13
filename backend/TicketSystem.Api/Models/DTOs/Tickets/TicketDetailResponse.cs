namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TicketDetailResponse
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;
    public string? StatusColor { get; set; }
    public string Priority { get; set; } = string.Empty;
    public int RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public int ApplicationId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public string ApplicationAlias { get; set; } = string.Empty;
    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public string? AssignedToDepartment { get; set; }
    public DateTime? SlaDeadline { get; set; }
    public DateTime? SlaBreachedAt { get; set; }
    public DateTime? ResolutionAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<MessageDto> Messages { get; set; } = new();
    public List<StatusHistoryDto> StatusHistory { get; set; } = new();
    public List<CorrectiveActionDto> CorrectiveActions { get; set; } = new();

    public class MessageDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public bool IsFromRequester { get; set; }
        public int CreatedById { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string? CreatedByEmail { get; set; }
        public string MessageSourceType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<AttachmentDto> Attachments { get; set; } = new();
    }

    public class AttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class StatusHistoryDto
    {
        public int Id { get; set; }
        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = string.Empty;
        public int ChangedById { get; set; }
        public string ChangedByName { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CorrectiveActionDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
