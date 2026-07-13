using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Models.DTOs.Notifications;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(TicketSystemDbContext context) : base(context) { }

    public async Task<NotificationListResponse> GetByUserAsync(int userId, bool? unreadOnly = null, int page = 1, int pageSize = 20)
    {
        var q = Context.Notifications.AsNoTracking()
            .Include(n => n.Ticket)
            .Where(n => n.UserId == userId);

        if (unreadOnly == true)
            q = q.Where(n => !n.IsRead);

        var total = await q.CountAsync();
        var unread = await q.CountAsync(n => !n.IsRead);

        var items = await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationResponse
            {
                Id = n.Id,
                UserId = n.UserId,
                TicketId = n.TicketId,
                TicketNumber = n.Ticket != null ? n.Ticket.TicketNumber : null,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return new NotificationListResponse
        {
            Items = items,
            TotalCount = total,
            UnreadCount = unread
        };
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        var notification = await DbSet.FindAsync(notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
            await Context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        await DbSet.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
