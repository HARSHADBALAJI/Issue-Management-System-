using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Models.DTOs.Sla;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class SlaService : ISlaService
{
    private readonly TicketSystemDbContext _context;
    private readonly SlaCalculationEngine _engine;
    private readonly ILogger<SlaService> _logger;

    public SlaService(TicketSystemDbContext context, SlaCalculationEngine engine, ILogger<SlaService> logger)
    {
        _context = context;
        _engine = engine;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<SlaSettingsDto> GetSettingsAsync()
    {
        var settings = await _engine.GetSettingsAsync();
        return new SlaSettingsDto
        {
            Id = settings.Id,
            WorkStartTime = settings.WorkStartTime.ToString(@"hh\:mm"),
            WorkEndTime = settings.WorkEndTime.ToString(@"hh\:mm"),
            NotifyBeforeHours = settings.NotifyBeforeHours
        };
    }

    public async Task UpdateSettingsAsync(UpdateSlaSettingsRequest request)
    {
        var settings = await _context.SlaSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SlaSetting();
            _context.SlaSettings.Add(settings);
        }

        settings.WorkStartTime = TimeSpan.Parse(request.WorkStartTime);
        settings.WorkEndTime = TimeSpan.Parse(request.WorkEndTime);
        settings.NotifyBeforeHours = request.NotifyBeforeHours;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _engine.InvalidateCache();
    }

    public async Task<List<HolidayDto>> GetHolidaysAsync()
    {
        return await _context.HolidayCalendars
            .AsNoTracking()
            .Where(h => h.IsActive)
            .OrderByDescending(h => h.Date)
            .Select(h => new HolidayDto
            {
                Id = h.Id,
                Date = h.Date.ToString("yyyy-MM-dd"),
                Name = h.Name
            })
            .ToListAsync();
    }

    public async Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request)
    {
        var holiday = new HolidayCalendar
        {
            Date = DateTime.Parse(request.Date).Date,
            Name = request.Name,
            IsActive = true
        };

        _context.HolidayCalendars.Add(holiday);
        await _context.SaveChangesAsync();
        _engine.InvalidateCache();

        return new HolidayDto
        {
            Id = holiday.Id,
            Date = holiday.Date.ToString("yyyy-MM-dd"),
            Name = holiday.Name
        };
    }

    public async Task UpdateHolidayAsync(int id, CreateHolidayRequest request)
    {
        var holiday = await _context.HolidayCalendars.FindAsync(id)
            ?? throw new KeyNotFoundException($"Holiday with id {id} not found.");

        holiday.Date = DateTime.Parse(request.Date).Date;
        holiday.Name = request.Name;
        await _context.SaveChangesAsync();
        _engine.InvalidateCache();
    }

    public async Task DeleteHolidayAsync(int id)
    {
        var holiday = await _context.HolidayCalendars.FindAsync(id)
            ?? throw new KeyNotFoundException($"Holiday with id {id} not found.");

        holiday.IsActive = false;
        await _context.SaveChangesAsync();
        _engine.InvalidateCache();
    }

    public async Task<List<WeeklyHolidayRuleDto>> GetWeeklyRulesAsync()
    {
        return await _context.WeeklyHolidayRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Select(r => new WeeklyHolidayRuleDto
            {
                Id = r.Id,
                DayOfWeek = r.DayOfWeek.ToString(),
                WeekType = r.WeekType,
                Description = r.Description
            })
            .ToListAsync();
    }

    public async Task<WeeklyHolidayRuleDto> CreateWeeklyRuleAsync(CreateWeeklyHolidayRuleRequest request)
    {
        var rule = new WeeklyHolidayRule
        {
            DayOfWeek = Enum.Parse<DayOfWeek>(request.DayOfWeek),
            WeekType = request.WeekType,
            Description = request.Description,
            IsActive = true
        };

        _context.WeeklyHolidayRules.Add(rule);
        await _context.SaveChangesAsync();
        _engine.InvalidateCache();

        return new WeeklyHolidayRuleDto
        {
            Id = rule.Id,
            DayOfWeek = rule.DayOfWeek.ToString(),
            WeekType = rule.WeekType,
            Description = rule.Description
        };
    }

    public async Task DeleteWeeklyRuleAsync(int id)
    {
        var rule = await _context.WeeklyHolidayRules.FindAsync(id)
            ?? throw new KeyNotFoundException($"Weekly holiday rule with id {id} not found.");

        rule.IsActive = false;
        await _context.SaveChangesAsync();
        _engine.InvalidateCache();
    }

    public async Task<List<SlaPolicyDto>> GetPoliciesAsync()
    {
        return await _context.SlaPolicies
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Priority)
            .Select(p => new SlaPolicyDto
            {
                Id = p.Id,
                Priority = p.Priority,
                DurationDays = p.DurationDays
            })
            .ToListAsync();
    }

    public async Task UpdatePolicyAsync(int id, UpdateSlaPolicyRequest request)
    {
        var policy = await _context.SlaPolicies.FindAsync(id)
            ?? throw new KeyNotFoundException($"SLA policy with id {id} not found.");

        policy.DurationDays = request.DurationDays;
        policy.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _engine.InvalidateCache();
    }

    public async Task<TicketSlaInfoDto?> GetTicketSlaAsync(int ticketId)
    {
        var sla = await _context.TicketSlas
            .AsNoTracking()
            .Include(s => s.SlaPolicy)
            .Where(s => s.TicketId == ticketId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (sla == null) return null;

        var settings = await _engine.GetSettingsAsync();
        var workDayHours = (settings.WorkEndTime - settings.WorkStartTime).TotalHours;

        string? remainingTime;
        int remainingPercent;
        string? overdueTime = null;

        if (sla.Status == SlaStatus.Completed || sla.Status == SlaStatus.Reopened)
        {
            remainingTime = "0h 0m";
            remainingPercent = 0;
        }
        else if (sla.Status == SlaStatus.Breached)
        {
            remainingTime = "0h 0m";
            remainingPercent = 0;
            var overdueSpan = DateTime.UtcNow - sla.DeadlineAt;
            overdueTime = FormatTimeSpan(overdueSpan);
        }
        else
        {
            var effectiveNow = sla.Status == SlaStatus.Paused && sla.PausedAt.HasValue
                ? sla.PausedAt.Value
                : DateTime.UtcNow;

            var remainingHours = await _engine.GetRemainingWorkingHoursAsync(effectiveNow, sla.DeadlineAt, sla.TotalPausedDuration);
            remainingTime = FormatTimeSpan(TimeSpan.FromHours(remainingHours));

            var totalHours = sla.SlaPolicy.DurationDays * workDayHours;
            remainingPercent = totalHours > 0
                ? Math.Clamp((int)Math.Round(remainingHours / totalHours * 100), 0, 100)
                : 0;
        }

        return new TicketSlaInfoDto
        {
            Id = sla.Id,
            TicketId = sla.TicketId,
            Priority = sla.Priority,
            Status = sla.Status.ToString(),
            StartedAt = sla.StartedAt?.ToString("o"),
            PausedAt = sla.PausedAt?.ToString("o"),
            PausedDuration = sla.TotalPausedDuration > TimeSpan.Zero ? FormatTimeSpan(sla.TotalPausedDuration) : null,
            DeadlineAt = sla.DeadlineAt.ToString("o"),
            CompletedAt = sla.CompletedAt?.ToString("o"),
            BreachedAt = sla.BreachedAt?.ToString("o"),
            RemainingTime = remainingTime,
            RemainingPercent = remainingPercent,
            OverdueTime = overdueTime
        };
    }

    public async Task<List<SlaAuditEntryDto>> GetSlaAuditAsync(int ticketId)
    {
        return await _context.SlaAuditLogs
            .AsNoTracking()
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new SlaAuditEntryDto
            {
                Id = a.Id,
                Action = a.Action.ToString(),
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                Description = a.Description,
                CreatedAt = a.CreatedAt.ToString("o")
            })
            .ToListAsync();
    }

    public async Task StartSlaAsync(int ticketId, string priority)
    {
        var hasActive = await _context.TicketSlas
            .AnyAsync(s => s.TicketId == ticketId && s.IsActive);

        if (hasActive)
        {
            _logger.LogWarning("Active SLA already exists for ticket {TicketId}", ticketId);
            return;
        }

        var policy = await _context.SlaPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Priority == priority && p.IsActive);

        if (policy == null)
            throw new InvalidOperationException($"No active SLA policy found for priority '{priority}'.");

        var now = DateTime.UtcNow;
        var deadline = await _engine.CalculateDeadlineAsync(now, policy.DurationDays);

        var sla = new TicketSla
        {
            TicketId = ticketId,
            SlaPolicyId = policy.Id,
            Priority = priority,
            Status = SlaStatus.Running,
            StartedAt = now,
            DeadlineAt = deadline,
            IsActive = true
        };

        _context.TicketSlas.Add(sla);
        await _context.SaveChangesAsync();

        await RecordAuditLogAsync(sla.Id, ticketId, SlaAction.Started,
            null,
            JsonSerializer.Serialize(new { priority, deadline = deadline.ToString("o"), policyId = policy.Id }, JsonOptions),
            $"SLA started with priority '{priority}', deadline {deadline:yyyy-MM-dd HH:mm} UTC");
    }

    public async Task PauseSlaAsync(int ticketId)
    {
        var sla = await GetActiveSlaAsync(ticketId);
        if (sla == null) return;

        if (sla.Status != SlaStatus.Running)
            throw new InvalidOperationException($"SLA is not running (current status: {sla.Status}).");

        var now = DateTime.UtcNow;
        sla.PausedAt = now;
        sla.Status = SlaStatus.Paused;
        sla.UpdatedAt = now;
        await _context.SaveChangesAsync();

        await RecordAuditLogAsync(sla.Id, ticketId, SlaAction.Paused,
            null,
            now.ToString("o"),
            "SLA paused");
    }

    public async Task ResumeSlaAsync(int ticketId)
    {
        var sla = await GetActiveSlaAsync(ticketId);
        if (sla == null) return;

        if (sla.Status != SlaStatus.Paused)
            return;

        var now = DateTime.UtcNow;
        if (sla.PausedAt.HasValue)
        {
            sla.TotalPausedDuration = sla.TotalPausedDuration.Add(now - sla.PausedAt.Value);
        }

        sla.PausedAt = null;
        sla.Status = SlaStatus.Running;
        sla.UpdatedAt = now;
        await _context.SaveChangesAsync();

        await RecordAuditLogAsync(sla.Id, ticketId, SlaAction.Resumed,
            null,
            null,
            $"SLA resumed, total paused duration: {FormatTimeSpan(sla.TotalPausedDuration)}");
    }

    public async Task CompleteSlaAsync(int ticketId)
    {
        var sla = await GetActiveSlaAsync(ticketId);
        if (sla == null) return;

        if (sla.Status != SlaStatus.Running && sla.Status != SlaStatus.Paused)
            throw new InvalidOperationException($"SLA cannot be completed (current status: {sla.Status}).");

        var now = DateTime.UtcNow;

        if (sla.Status == SlaStatus.Paused && sla.PausedAt.HasValue)
        {
            sla.TotalPausedDuration = sla.TotalPausedDuration.Add(now - sla.PausedAt.Value);
            sla.PausedAt = null;
        }

        sla.Status = SlaStatus.Completed;
        sla.CompletedAt = now;
        sla.UpdatedAt = now;
        await _context.SaveChangesAsync();

        await RecordAuditLogAsync(sla.Id, ticketId, SlaAction.Completed,
            null,
            now.ToString("o"),
            "SLA completed");
    }

    public async Task ReopenSlaAsync(int ticketId)
    {
        var oldSla = await GetActiveSlaAsync(ticketId);

        if (oldSla != null)
        {
            var escalatedPriority = EscalatePriority(oldSla.Priority);

            oldSla.IsActive = false;
            oldSla.Status = SlaStatus.Reopened;
            oldSla.UpdatedAt = DateTime.UtcNow;

            var policy = await _context.SlaPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Priority == escalatedPriority && p.IsActive);

            if (policy == null)
                throw new InvalidOperationException($"No active SLA policy found for escalated priority '{escalatedPriority}'.");

            var now = DateTime.UtcNow;
            var deadline = await _engine.CalculateDeadlineAsync(now, policy.DurationDays);

            var newSla = new TicketSla
            {
                TicketId = ticketId,
                SlaPolicyId = policy.Id,
                Priority = escalatedPriority,
                Status = SlaStatus.Running,
                StartedAt = now,
                DeadlineAt = deadline,
                IsActive = true
            };

            _context.TicketSlas.Add(newSla);
            await _context.SaveChangesAsync();

            await RecordAuditLogAsync(oldSla.Id, ticketId, SlaAction.Reopened,
                JsonSerializer.Serialize(new { oldPriority = oldSla.Priority }, JsonOptions),
                JsonSerializer.Serialize(new { newPriority = escalatedPriority }, JsonOptions),
                $"SLA reopened with escalated priority '{escalatedPriority}'");

            await RecordAuditLogAsync(newSla.Id, ticketId, SlaAction.Started,
                null,
                JsonSerializer.Serialize(new { priority = escalatedPriority, deadline = deadline.ToString("o") }, JsonOptions),
                $"SLA started with priority '{escalatedPriority}', deadline {deadline:yyyy-MM-dd HH:mm} UTC");
        }
        else
        {
            // No existing SLA — create one from scratch for backwards-compatible tickets
            var ticket = await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
                throw new InvalidOperationException($"Ticket {ticketId} not found.");

            var priority = ticket.Priority ?? "low";
            var policy = await _context.SlaPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Priority == priority && p.IsActive);

            if (policy == null)
                throw new InvalidOperationException($"No active SLA policy found for priority '{priority}'.");

            var now = DateTime.UtcNow;
            var deadline = await _engine.CalculateDeadlineAsync(now, policy.DurationDays);

            var newSla = new TicketSla
            {
                TicketId = ticketId,
                SlaPolicyId = policy.Id,
                Priority = priority,
                Status = SlaStatus.Running,
                StartedAt = now,
                DeadlineAt = deadline,
                IsActive = true
            };

            _context.TicketSlas.Add(newSla);
            await _context.SaveChangesAsync();

            await RecordAuditLogAsync(newSla.Id, ticketId, SlaAction.Started,
                null,
                JsonSerializer.Serialize(new { priority, deadline = deadline.ToString("o") }, JsonOptions),
                $"SLA started with priority '{priority}', deadline {deadline:yyyy-MM-dd HH:mm} UTC");
        }
    }

    public async Task CheckBreachedSlasAsync()
    {
        var now = DateTime.UtcNow;

        var breachedSlas = await _context.TicketSlas
            .Where(s => s.IsActive && s.Status == SlaStatus.Running && s.DeadlineAt <= now)
            .ToListAsync();

        if (breachedSlas.Count == 0) return;

        foreach (var sla in breachedSlas)
        {
            sla.Status = SlaStatus.Breached;
            sla.BreachedAt = now;
            sla.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        foreach (var sla in breachedSlas)
        {
            await RecordAuditLogAsync(sla.Id, sla.TicketId, SlaAction.Breached,
                null,
                now.ToString("o"),
                $"SLA breached - deadline was {sla.DeadlineAt:yyyy-MM-dd HH:mm} UTC");
        }

        _logger.LogInformation("Marked {Count} SLA(s) as breached", breachedSlas.Count);
    }

    public async Task SendSlaNotificationsAsync()
    {
        var settings = await _engine.GetSettingsAsync();
        var now = DateTime.UtcNow;

        var runningSlas = await _context.TicketSlas
            .AsNoTracking()
            .Where(s => s.IsActive && s.Status == SlaStatus.Running && s.DeadlineAt > now)
            .ToListAsync();

        var nearBreach = new List<(TicketSla Sla, double RemainingHours)>();

        foreach (var sla in runningSlas)
        {
            var remainingHours = await _engine.GetRemainingWorkingHoursAsync(now, sla.DeadlineAt, sla.TotalPausedDuration);
            if (remainingHours <= settings.NotifyBeforeHours)
            {
                nearBreach.Add((sla, remainingHours));
            }
        }

        if (nearBreach.Count == 0) return;

        foreach (var (sla, remainingHours) in nearBreach)
        {
            var audit = new SlaAuditLog
            {
                TicketSlaId = sla.Id,
                TicketId = sla.TicketId,
                Action = SlaAction.NotificationSent,
                Description = $"SLA nearing breach: {FormatTimeSpan(TimeSpan.FromHours(remainingHours))} remaining",
                CreatedAt = DateTime.UtcNow
            };
            _context.SlaAuditLogs.Add(audit);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Sent SLA breach notifications for {Count} ticket(s)", nearBreach.Count);
    }

    private async Task<TicketSla?> GetActiveSlaAsync(int ticketId)
    {
        return await _context.TicketSlas
            .Where(s => s.TicketId == ticketId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private async Task RecordAuditLogAsync(int ticketSlaId, int ticketId, SlaAction action, string? oldValue, string? newValue, string description)
    {
        var audit = new SlaAuditLog
        {
            TicketSlaId = ticketSlaId,
            TicketId = ticketId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        _context.SlaAuditLogs.Add(audit);
        await _context.SaveChangesAsync();
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours < 0) return "0h 0m";
        var totalDays = (int)ts.TotalDays;
        var hours = ts.Hours;
        var minutes = ts.Minutes;
        if (totalDays > 0)
            return $"{totalDays}d {hours}h {minutes}m";
        return $"{hours}h {minutes}m";
    }

    private static string EscalatePriority(string currentPriority)
    {
        return currentPriority.ToLowerInvariant() switch
        {
            "low" => "medium",
            "medium" => "high",
            "high" => "critical",
            "critical" => "critical",
            _ => "medium"
        };
    }
}
