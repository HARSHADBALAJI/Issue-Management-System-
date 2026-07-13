using TicketSystem.Api.Models.DTOs.Sla;

namespace TicketSystem.Api.Services;

public interface ISlaService
{
    Task<SlaSettingsDto> GetSettingsAsync();
    Task UpdateSettingsAsync(UpdateSlaSettingsRequest request);

    Task<List<HolidayDto>> GetHolidaysAsync();
    Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request);
    Task UpdateHolidayAsync(int id, CreateHolidayRequest request);
    Task DeleteHolidayAsync(int id);

    Task<List<WeeklyHolidayRuleDto>> GetWeeklyRulesAsync();
    Task<WeeklyHolidayRuleDto> CreateWeeklyRuleAsync(CreateWeeklyHolidayRuleRequest request);
    Task DeleteWeeklyRuleAsync(int id);

    Task<List<SlaPolicyDto>> GetPoliciesAsync();
    Task UpdatePolicyAsync(int id, UpdateSlaPolicyRequest request);

    Task<TicketSlaInfoDto?> GetTicketSlaAsync(int ticketId);
    Task<List<SlaAuditEntryDto>> GetSlaAuditAsync(int ticketId);

    Task StartSlaAsync(int ticketId, string priority);
    Task PauseSlaAsync(int ticketId);
    Task ResumeSlaAsync(int ticketId);
    Task CompleteSlaAsync(int ticketId);
    Task ReopenSlaAsync(int ticketId);
    Task CheckBreachedSlasAsync();
    Task SendSlaNotificationsAsync();
}
