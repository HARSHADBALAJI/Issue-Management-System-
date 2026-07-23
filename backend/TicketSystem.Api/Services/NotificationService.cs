using Microsoft.AspNetCore.SignalR;
using TicketSystem.Api.Data.Repositories;
using TicketSystem.Api.Hubs;
using TicketSystem.Api.Models.DTOs.Notifications;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifRepo;
    private readonly IHubContext<TicketHub> _hubContext;

    public NotificationService(INotificationRepository notifRepo, IHubContext<TicketHub> hubContext)
    {
        _notifRepo = notifRepo;
        _hubContext = hubContext;
    }

    public async Task<NotificationListResponse> GetByUserAsync(int userId, bool? unreadOnly = null, int page = 1, int pageSize = 20)
        => await _notifRepo.GetByUserAsync(userId, unreadOnly, page, pageSize);

    public async Task MarkAsReadAsync(int notificationId)
        => await _notifRepo.MarkAsReadAsync(notificationId);

    public async Task MarkAllAsReadAsync(int userId)
        => await _notifRepo.MarkAllAsReadAsync(userId);

    public async Task CreateAsync(int userId, int? ticketId, string type, string title, string? message = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            TicketId = ticketId,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false
        };
        await _notifRepo.AddAsync(notification);

        try
        {
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("NotificationReceived", new
            {
                notification.Id,
                notification.Title,
                notification.Message,
                notification.Type,
                notification.TicketId,
                notification.CreatedAt
            });
        }
        catch
        {
            // SignalR send failures should not break notification creation
        }
    }
}
