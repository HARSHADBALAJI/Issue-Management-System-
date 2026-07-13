using TicketSystem.Api.Models.DTOs.Notifications;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public interface INotificationRepository : IRepository<Notification>
{
    Task<NotificationListResponse> GetByUserAsync(int userId, bool? unreadOnly = null, int page = 1, int pageSize = 20);
    Task MarkAsReadAsync(int notificationId);
    Task MarkAllAsReadAsync(int userId);
}
