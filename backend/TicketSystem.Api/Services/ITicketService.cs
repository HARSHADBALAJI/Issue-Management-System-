using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Tickets;

namespace TicketSystem.Api.Services;

public interface ITicketService
{
    Task<PagedResponse<TicketListResponse>> GetPagedAsync(TicketQueryParams query);
    Task<TicketDetailResponse?> GetByIdAsync(int id);
    Task<TicketResponse> CreateAsync(CreateTicketRequest request);
    Task<TicketMessageResponse> AddMessageAsync(int ticketId, CreateTicketMessageRequest request, int userId, List<Microsoft.AspNetCore.Http.IFormFile>? files = null);
    Task<CorrectiveActionResponse> AddCorrectiveActionAsync(int ticketId, CreateCorrectiveActionRequest request, int userId);
    Task AssignAsync(int ticketId, AssignTicketRequest request, int userId);
    Task UpdateStatusAsync(int ticketId, UpdateTicketStatusRequest request, int userId);
    Task<TicketStatsResponse> GetStatsAsync(TicketStatsQueryParams query);
    Task<List<TicketSlaSummary>> GetSlaSummaryAsync(int? userId = null);
    Task BulkAssignAsync(BulkAssignRequest request);
    Task<BulkResponse> BulkUpdateStatusAsync(BulkStatusRequest request);
    Task ReopenAsync(int ticketId, int userId);
}
