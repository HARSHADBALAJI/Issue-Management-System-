namespace TicketSystem.Api.Services;

public interface IEmailOutboxService
{
    Task EnqueueAsync(string recipientEmail, string subject, string bodyHtml, string? bodyPlainText,
        int? ticketId = null, int? requesterId = null, int? userId = null,
        string? inReplyTo = null, string? references = null,
        string? senderEmail = null, string? recipientName = null,
        int? ticketMessageId = null);
}
