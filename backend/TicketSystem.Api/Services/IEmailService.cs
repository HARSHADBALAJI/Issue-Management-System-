using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public interface IEmailService
{
    Task SendTicketCreatedEmailAsync(Ticket ticket, Requester requester, string? spocEmail = null);
    Task SendAssignedEmailAsync(Ticket ticket, Requester requester, User spoc);
    Task SendReassignedEmailAsync(Ticket ticket, Requester requester, User newSpoc);
    Task SendSpocReplyEmailAsync(Ticket ticket, TicketMessage message, Requester requester, User spoc);
    Task SendRequesterReplyEmailAsync(Ticket ticket, TicketMessage message, Requester requester, User spoc);
    Task SendWaitingEmailAsync(Ticket ticket, Requester requester, User? spoc);
    Task SendResolvedEmailAsync(Ticket ticket, Requester requester);
    Task SendClosedEmailAsync(Ticket ticket, Requester requester);
    Task SendReopenedEmailAsync(Ticket ticket, Requester requester, User? spoc);
    Task SendAutoCloseEmailAsync(Ticket ticket, Requester requester);
    Task SendStatusChangeEmailAsync(Ticket ticket, string fromStatus, string toStatus);
    Task SendMessageNotificationAsync(Ticket ticket, TicketMessage message, string recipientEmail, string? senderName = null);
    Task<(bool success, string? error)> SendReplyAsync(string toEmail, string subject, string body, string? inReplyTo = null, string? references = null);
}
