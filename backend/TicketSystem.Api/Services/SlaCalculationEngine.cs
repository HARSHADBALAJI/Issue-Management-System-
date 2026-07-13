using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class SlaCalculationEngine
{
    private readonly TicketSystemDbContext _context;
    private readonly ILogger<SlaCalculationEngine> _logger;
    private SlaSetting? _cachedSettings;
    private DateTime _cacheTimestamp;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SlaCalculationEngine(TicketSystemDbContext context, ILogger<SlaCalculationEngine> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SlaSetting> GetSettingsAsync()
    {
        if (_cachedSettings != null && DateTime.UtcNow - _cacheTimestamp < CacheDuration)
            return _cachedSettings;

        _cachedSettings = await _context.SlaSettings.AsNoTracking().FirstOrDefaultAsync()
            ?? new SlaSetting();
        _cacheTimestamp = DateTime.UtcNow;
        return _cachedSettings;
    }

    public void InvalidateCache() { _cachedSettings = null; }

    public async Task<DateTime> CalculateDeadlineAsync(DateTime start, int workingDays)
    {
        var settings = await GetSettingsAsync();
        var workStart = settings.WorkStartTime;
        var workEnd = settings.WorkEndTime;
        var workDayHours = (workEnd - workStart).TotalHours;

        var remainingHours = workingDays * workDayHours;
        var current = start;

        if (!await IsWorkingDayAsync(current))
        {
            current = await GetNextWorkingDayStartAsync(current, workStart);
        }
        else
        {
            var timeOfDay = current.TimeOfDay;
            if (timeOfDay < workStart)
                current = current.Date + workStart;
            else if (timeOfDay >= workEnd)
                current = await GetNextWorkingDayStartAsync(current, workStart);
        }

        while (remainingHours > 0)
        {
            if (!await IsWorkingDayAsync(current))
            {
                current = await GetNextWorkingDayStartAsync(current, workStart);
                continue;
            }

            var dayStart = current.Date + workStart;
            var dayEnd = current.Date + workEnd;
            var availableToday = (dayEnd - current).TotalHours;

            if (availableToday <= 0)
            {
                current = await GetNextWorkingDayStartAsync(current, workStart);
                continue;
            }

            if (availableToday >= remainingHours)
            {
                current = current.AddHours(remainingHours);
                remainingHours = 0;
            }
            else
            {
                remainingHours -= availableToday;
                current = await GetNextWorkingDayStartAsync(current, workStart);
            }
        }

        return current;
    }

    public async Task<double> GetRemainingWorkingHoursAsync(DateTime now, DateTime deadline, TimeSpan pausedDuration)
    {
        var effectiveNow = now;
        var settings = await GetSettingsAsync();
        var workStart = settings.WorkStartTime;
        var workEnd = settings.WorkEndTime;
        var workDayHours = (workEnd - workStart).TotalHours;

        if (effectiveNow >= deadline) return 0;

        double totalHours = 0;
        var current = effectiveNow;

        if (!await IsWorkingDayAsync(current))
        {
            current = await GetNextWorkingDayStartAsync(current, workStart);
        }
        else
        {
            var timeOfDay = current.TimeOfDay;
            if (timeOfDay < workStart)
                current = current.Date + workStart;
            else if (timeOfDay >= workEnd)
            {
                current = await GetNextWorkingDayStartAsync(current, workStart);
            }
        }

        while (current < deadline)
        {
            if (!await IsWorkingDayAsync(current))
            {
                current = await GetNextWorkingDayStartAsync(current, workStart);
                continue;
            }

            var dayStart = current.Date + workStart;
            var dayEnd = current.Date + workEnd;
            var dayDeadline = deadline < dayEnd ? deadline : dayEnd;

            if (current < dayDeadline)
            {
                totalHours += (dayDeadline - current).TotalHours;
            }

            current = await GetNextWorkingDayStartAsync(current, workStart);
        }

        return Math.Max(0, totalHours);
    }

    public async Task<bool> IsWorkingDayAsync(DateTime date)
    {
        var isWeeklyHoliday = await IsWeeklyHolidayAsync(date);
        if (isWeeklyHoliday) return false;

        var isGovHoliday = await _context.HolidayCalendars
            .AsNoTracking()
            .AnyAsync(h => h.IsActive && h.Date == date.Date);

        return !isGovHoliday;
    }

    private async Task<bool> IsWeeklyHolidayAsync(DateTime date)
    {
        var rules = await _context.WeeklyHolidayRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync();

        foreach (var rule in rules)
        {
            if (rule.DayOfWeek != date.DayOfWeek) continue;

            if (rule.WeekType == "All") return true;

            var dayOfMonth = date.Day;
            var weekOfMonth = (dayOfMonth - 1) / 7 + 1;

            if (rule.WeekType == "Second" && weekOfMonth == 2) return true;
            if (rule.WeekType == "Fourth" && weekOfMonth == 4) return true;
            if (rule.WeekType == "EverySecondAndFourth" && (weekOfMonth == 2 || weekOfMonth == 4)) return true;
        }

        return false;
    }

    private async Task<DateTime> GetNextWorkingDayStartAsync(DateTime from, TimeSpan workStart)
    {
        var next = from.Date.AddDays(1);
        while (!await IsWorkingDayAsync(next))
        {
            next = next.AddDays(1);
        }
        return next + workStart;
    }
}
