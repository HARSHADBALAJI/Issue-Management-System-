using TicketSystem.Api.Models.DTOs.Notifications;

namespace TicketSystem.Api.Services;

public interface INotificationService
{
    Task<NotificationListResponse> GetByUserAsync(int userId, bool? unreadOnly = null, int page = 1, int pageSize = 20);
    Task MarkAsReadAsync(int notificationId);
    Task MarkAllAsReadAsync(int userId);
    Task CreateAsync(int userId, int? ticketId, string type, string title, string? message = null);
}
