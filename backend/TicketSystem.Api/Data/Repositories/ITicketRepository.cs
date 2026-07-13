using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Tickets;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public interface ITicketRepository : IRepository<Ticket>
{
    Task<Ticket?> GetByIdWithIncludesAsync(int id);
    Task<PagedResponse<TicketListResponse>> GetPagedAsync(TicketQueryParams query);
    Task<TicketDetailResponse?> GetDetailAsync(int id);
    Task<TicketListResponse?> GetTicketListResponseAsync(int id);
    Task<int> GetNextSequenceAsync();
    Task<TicketStatsResponse> GetStatsAsync(TicketStatsQueryParams query);
    Task<List<TicketSlaSummary>> GetSlaSummaryAsync();
    Task BulkAssignAsync(List<int> ticketIds, int assignedToUserId);
    Task BulkUpdateStatusAsync(List<int> ticketIds, int statusId);
    Task<TicketMessage> AddMessageAsync(TicketMessage message);
    Task<TicketStatusHistory> AddStatusHistoryAsync(TicketStatusHistory history);
    Task<TicketCorrectiveAction> AddCorrectiveActionAsync(TicketCorrectiveAction action);
}
