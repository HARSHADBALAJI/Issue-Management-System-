namespace TicketSystem.Api.Models.DTOs.Sla;

public class SlaSettingsDto
{
    public int Id { get; set; }
    public string WorkStartTime { get; set; } = "09:00";
    public string WorkEndTime { get; set; } = "17:40";
    public int NotifyBeforeHours { get; set; } = 24;
}

public class UpdateSlaSettingsRequest
{
    public string WorkStartTime { get; set; } = "09:00";
    public string WorkEndTime { get; set; } = "17:40";
    public int NotifyBeforeHours { get; set; } = 24;
}

public class HolidayDto
{
    public int Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class CreateHolidayRequest
{
    public string Date { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class WeeklyHolidayRuleDto
{
    public int Id { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public string WeekType { get; set; } = "All";
    public string Description { get; set; } = string.Empty;
}

public class CreateWeeklyHolidayRuleRequest
{
    public string DayOfWeek { get; set; } = string.Empty;
    public string WeekType { get; set; } = "All";
    public string Description { get; set; } = string.Empty;
}

public class SlaPolicyDto
{
    public int Id { get; set; }
    public string Priority { get; set; } = string.Empty;
    public int DurationDays { get; set; }
}

public class UpdateSlaPolicyRequest
{
    public int DurationDays { get; set; }
}

public class TicketSlaInfoDto
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StartedAt { get; set; }
    public string? PausedAt { get; set; }
    public string? PausedDuration { get; set; }
    public string DeadlineAt { get; set; } = string.Empty;
    public string? CompletedAt { get; set; }
    public string? BreachedAt { get; set; }
    public string? RemainingTime { get; set; }
    public int RemainingPercent { get; set; }
}

public class SlaAuditEntryDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}
