namespace TicketSystem.Api.Models.DTOs.Notifications;

public class NotificationListResponse
{
    public List<NotificationResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
}
