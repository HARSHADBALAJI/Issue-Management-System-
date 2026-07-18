using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly TicketSystemDbContext _context;
    private readonly IEmailOutboxService _outbox;
    private readonly string _systemEmail;
    private readonly string _templatePath;
    private readonly Dictionary<string, string> _templateCache = new();

    public EmailService(IConfiguration config, ILogger<EmailService> logger, TicketSystemDbContext context, IEmailOutboxService outbox)
    {
        _logger = logger;
        _context = context;
        _outbox = outbox;
        _systemEmail = Environment.GetEnvironmentVariable("SYSTEM_USER_EMAIL") ?? config["Email:SystemEmail"] ?? "noreply@ticketingsystem.com";

        _templatePath = FindTemplatePath();
        _logger.LogInformation("Email template path resolved to: {Path}", _templatePath);
    }

    private string FindTemplatePath()
    {
        // 1. AppContext.BaseDirectory/Templates/Email (standard publish output)
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Templates", "Email"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "Email"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Templates", "Email"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "backend", "TicketSystem.Api", "Templates", "Email"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TicketSystem.Api", "Templates", "Email"),
        };

        foreach (var c in candidates)
        {
            var resolved = Path.GetFullPath(c);
            if (Directory.Exists(resolved))
            {
                _logger.LogInformation("Found email templates at: {Path}", resolved);
                return resolved;
            }
        }

        // 2. Walk up from current directory looking for Templates/Email
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 10 && dir != null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "Templates", "Email");
            if (Directory.Exists(candidate))
            {
                _logger.LogInformation("Found email templates by walk-up at: {Path}", candidate);
                return candidate;
            }
            dir = dir.Parent;
        }

        _logger.LogWarning("Email template path not found in any candidate location");
        return Path.Combine(AppContext.BaseDirectory, "Templates", "Email");
    }

    private string LoadTemplate(string name)
    {
        if (_templateCache.TryGetValue(name, out var cached))
            return cached;
        var path = Path.Combine(_templatePath, $"{name}.html");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Email template {Path} not found, falling back to plain text", path);
            return null!;
        }
        var content = File.ReadAllText(path);
        _templateCache[name] = content;
        return content;
    }

    private string PopulateTemplate(string template, Dictionary<string, string> placeholders)
    {
        return placeholders.Aggregate(template, (current, kv) =>
            current.Replace($"{{{{{kv.Key}}}}}", kv.Value));
    }

    private async Task<string> GetApplicationNameAsync(Ticket ticket)
    {
        if (ticket.Application != null)
            return ticket.Application.Name;
        var app = await _context.Applications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == ticket.ApplicationId);
        return app?.Name ?? "";
    }

    private string WrapPlainText(string title, string text)
    {
        return $@"<!DOCTYPE html><html lang=""en""><head><meta charset=""UTF-8"" /><meta name=""viewport"" content=""width=device-width,initial-scale=1.0"" /></head><body style=""margin:0;padding:0;background-color:#F4F5F7;font-family:'Segoe UI',Arial,sans-serif;font-size:14px;line-height:1.6;color:#333333;""><table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#F4F5F7;padding:20px 0;""><tr><td align=""center""><table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;""><tr><td style=""background-color:#005BAC;border-radius:10px 10px 0 0;padding:28px 40px;""><table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td style=""color:#FFFFFF;font-size:20px;font-weight:600;"">Issue Management System</td></tr></table></td></tr><tr><td style=""background-color:#FFFFFF;padding:36px 40px;border-left:1px solid #E0E0E0;border-right:1px solid #E0E0E0;""><p style=""margin:0 0 16px 0;color:#333333;"">{title}</p>{text}</td></tr><tr><td style=""background-color:#F8F9FA;border-radius:0 0 10px 10px;padding:18px 40px;border:1px solid #E0E0E0;border-top:none;text-align:center;font-size:11px;color:#999999;line-height:1.5;"">This is an automated notification generated by the Issue Management System.</td></tr></table></td></tr></table></body></html>";
    }

    public async Task SendTicketCreatedEmailAsync(Ticket ticket, Requester requester, string? spocEmail = null)
    {
        var appName = await GetApplicationNameAsync(ticket);
        var template = LoadTemplate("TicketCreated");
        if (template != null)
        {
            var subject = "Support Request Received | Ticket #" + ticket.TicketNumber;
            var body = PopulateTemplate(template, new Dictionary<string, string>
            {
                ["UserName"] = requester.FullName,
                ["TicketId"] = ticket.TicketNumber ?? ticket.Id.ToString(),
                ["Subject"] = ticket.Subject ?? "",
                ["ApplicationName"] = appName,
                ["CreatedDate"] = DateTime.Now.ToString("dd-MM-yyyy HH:mm")
            });
            var msgId = await SendEmailAsync(requester.Email, subject, body, ticket.TicketNumber);
            await SaveOutgoingEmailAsync(null, requester.Id, subject, requester.Email, _systemEmail, "Sent", ticket.Id, msgId);
        }
        else
        {
            var fallbackBody = $"Dear {requester.FullName},\n\nYour ticket has been created successfully.\n\nTicket Number: {ticket.TicketNumber}\nSubject: {ticket.Subject}\n\nThank you,\nTicket Support Team";
            var msgId = await SendEmailAsync(requester.Email, fallbackBody, fallbackBody, ticket.TicketNumber);
            await SaveOutgoingEmailAsync(null, requester.Id, fallbackBody, requester.Email, _systemEmail, "Sent", ticket.Id, msgId);
        }

        if (!string.IsNullOrWhiteSpace(spocEmail))
        {
            var spocTemplate = LoadTemplate("NewTicketRaised");
            if (spocTemplate != null)
            {
                var spocSubject = "New Ticket Raised | Ticket #" + ticket.TicketNumber;
                var spocBody = PopulateTemplate(spocTemplate, new Dictionary<string, string>
                {
                    ["UserName"] = spocEmail,
                    ["TicketId"] = ticket.TicketNumber ?? ticket.Id.ToString(),
                    ["ApplicationName"] = appName,
                    ["Subject"] = ticket.Subject ?? "",
                    ["RaisedBy"] = requester.FullName,
                    ["CreatedDate"] = DateTime.Now.ToString("dd-MM-yyyy HH:mm")
                });
                var spocMsgId = await SendEmailAsync(spocEmail, spocSubject, spocBody, ticket.TicketNumber);
                await SaveOutgoingEmailAsync(null, requester.Id, spocSubject, spocEmail, _systemEmail, "Sent", ticket.Id, spocMsgId);
            }
            else
            {
                var fallbackBody = $"Dear SPOC,\n\nA new ticket has been assigned to you.\n\nTicket Number: {ticket.TicketNumber}\nSubject: {ticket.Subject}\nRequester: {requester.FullName}\n\nThank you,\nTicket System";
                var spocMsgId = await SendEmailAsync(spocEmail, fallbackBody, fallbackBody, ticket.TicketNumber);
                await SaveOutgoingEmailAsync(null, requester.Id, fallbackBody, spocEmail, _systemEmail, "Sent", ticket.Id, spocMsgId);
            }
        }
    }

    public async Task SendAssignedEmailAsync(Ticket ticket, Requester requester, User spoc)
    {
        var subject = $"Ticket Assigned | #{ticket.TicketNumber}";
        var title = $"Dear {requester.FullName},";
        var text = $@"<p style=""margin:0 0 16px 0;color:#333333;"">Your ticket has been assigned to a support representative.</p>
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width:100%;border:1px solid #E0E0E0;border-radius:8px;margin-bottom:24px;""><tr><td style=""padding:20px 24px;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;"">Ticket ID</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#005BAC;font-weight:600;vertical-align:top;"">#{ticket.TicketNumber}</td></tr>
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;border-top:1px solid #F0F0F0;"">Subject</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#333333;vertical-align:top;border-top:1px solid #F0F0F0;"">{ticket.Subject}</td></tr>
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;border-top:1px solid #F0F0F0;"">Assigned To</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#333333;vertical-align:top;border-top:1px solid #F0F0F0;"">{spoc.FullName}</td></tr>
</table></td></tr></table>
<p style=""margin:0 0 12px 0;color:#333333;"">We will look into this and get back to you at the earliest.</p>
<p style=""margin:0 0 4px 0;color:#333333;"">Kind regards,</p>
<p style=""margin:0;color:#333333;font-weight:600;"">Issue Management System<br />Larsen &amp; Toubro Limited</p>";
        var body = WrapPlainText(title, text);
        var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
        var msgId = await SendEmailAsync(requester.Email, subject, body, ticket.TicketNumber, inReplyTo, references);
        await SaveOutgoingEmailAsync(spoc.Id, requester.Id, subject, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
    }

    public async Task SendReassignedEmailAsync(Ticket ticket, Requester requester, User newSpoc)
    {
        var subject = $"Ticket Reassigned | #{ticket.TicketNumber}";
        var title = $"Dear {requester.FullName},";
        var text = $@"<p style=""margin:0 0 16px 0;color:#333333;"">Your ticket has been reassigned to a new support representative.</p>
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width:100%;border:1px solid #E0E0E0;border-radius:8px;margin-bottom:24px;""><tr><td style=""padding:20px 24px;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;"">Ticket ID</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#005BAC;font-weight:600;vertical-align:top;"">#{ticket.TicketNumber}</td></tr>
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;border-top:1px solid #F0F0F0;"">Subject</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#333333;vertical-align:top;border-top:1px solid #F0F0F0;"">{ticket.Subject}</td></tr>
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;border-top:1px solid #F0F0F0;"">New Assigned To</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#333333;vertical-align:top;border-top:1px solid #F0F0F0;"">{newSpoc.FullName}</td></tr>
</table></td></tr></table>
<p style=""margin:0 0 12px 0;color:#333333;"">Please continue to monitor your ticket for updates.</p>
<p style=""margin:0 0 4px 0;color:#333333;"">Kind regards,</p>
<p style=""margin:0;color:#333333;font-weight:600;"">Issue Management System<br />Larsen &amp; Toubro Limited</p>";
        var body = WrapPlainText(title, text);
        var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
        var msgId = await SendEmailAsync(requester.Email, subject, body, ticket.TicketNumber, inReplyTo, references);
        await SaveOutgoingEmailAsync(newSpoc.Id, requester.Id, subject, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
    }

    public async Task SendSpocReplyEmailAsync(Ticket ticket, TicketMessage message, Requester requester, User spoc)
    {
        var template = LoadTemplate("TicketConversation");
        if (template != null)
        {
            var appName = await GetApplicationNameAsync(ticket);
            var subject = "New Update on Your Ticket | #" + ticket.TicketNumber;
            var (imagesHtml, inlineAttachments) = await BuildInlineImagesAsync(message.Id, ticket.Id);
            var body = PopulateTemplate(template, new Dictionary<string, string>
            {
                ["RecipientName"] = requester.FullName,
                ["TicketId"] = ticket.TicketNumber ?? ticket.Id.ToString(),
                ["ApplicationName"] = appName,
                ["Subject"] = ticket.Subject ?? "",
                ["SenderName"] = spoc.FullName,
                ["MessageDate"] = (message.CreatedAt).ToString("dd-MM-yyyy HH:mm"),
                ["Message"] = message.Content ?? "",
                ["ImageAttachments"] = imagesHtml
            });
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, subject, body, ticket.TicketNumber, inReplyTo, references, ticketMessageId: message.Id, inlineAttachments: inlineAttachments.Count > 0 ? inlineAttachments : null);
            await SaveOutgoingEmailAsync(spoc.Id, requester.Id, subject, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
        else
        {
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, $"Re: {ticket.Subject}", message.Content ?? "", ticket.TicketNumber, inReplyTo, references, ticketMessageId: message.Id);
            await SaveOutgoingEmailAsync(spoc.Id, requester.Id, $"Re: {ticket.Subject}", requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
    }

    public async Task SendRequesterReplyEmailAsync(Ticket ticket, TicketMessage message, Requester requester, User spoc)
    {
        var template = LoadTemplate("TicketConversation");
        if (template != null)
        {
            var appName = await GetApplicationNameAsync(ticket);
            var subject = "New Update on Your Ticket | #" + ticket.TicketNumber;
            var (imagesHtml, inlineAttachments) = await BuildInlineImagesAsync(message.Id, ticket.Id);
            var body = PopulateTemplate(template, new Dictionary<string, string>
            {
                ["RecipientName"] = spoc.FullName,
                ["TicketId"] = ticket.TicketNumber ?? ticket.Id.ToString(),
                ["ApplicationName"] = appName,
                ["Subject"] = ticket.Subject ?? "",
                ["SenderName"] = requester.FullName,
                ["MessageDate"] = (message.CreatedAt).ToString("dd-MM-yyyy HH:mm"),
                ["Message"] = message.Content ?? "",
                ["ImageAttachments"] = imagesHtml
            });
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(spoc.Email, subject, body, ticket.TicketNumber, inReplyTo, references, ticketMessageId: message.Id, inlineAttachments: inlineAttachments.Count > 0 ? inlineAttachments : null);
            await SaveOutgoingEmailAsync(spoc.Id, requester.Id, subject, spoc.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
        else
        {
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(spoc.Email, $"Re: {ticket.Subject}", message.Content ?? "", ticket.TicketNumber, inReplyTo, references, ticketMessageId: message.Id);
            await SaveOutgoingEmailAsync(spoc.Id, requester.Id, $"Re: {ticket.Subject}", spoc.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
    }

    public async Task SendWaitingEmailAsync(Ticket ticket, Requester requester, User? spoc)
    {
        var subject = $"Ticket Waiting for Information | #{ticket.TicketNumber}";
        var title = $"Dear {requester.FullName},";
        var text = $@"<p style=""margin:0 0 16px 0;color:#333333;"">Your ticket is now waiting for additional information from you.</p>
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width:100%;border:1px solid #E0E0E0;border-radius:8px;margin-bottom:24px;""><tr><td style=""padding:20px 24px;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;"">Ticket ID</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#005BAC;font-weight:600;vertical-align:top;"">#{ticket.TicketNumber}</td></tr>
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;border-top:1px solid #F0F0F0;"">Subject</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#333333;vertical-align:top;border-top:1px solid #F0F0F0;"">{ticket.Subject}</td></tr>
</table></td></tr></table>
<p style=""margin:0 0 12px 0;color:#333333;"">Please provide the required information at your earliest convenience so we can continue working on your request.</p>
<p style=""margin:0 0 4px 0;color:#333333;"">Kind regards,</p>
<p style=""margin:0;color:#333333;font-weight:600;"">Issue Management System<br />Larsen &amp; Toubro Limited</p>";
        var body = WrapPlainText(title, text);
        var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
        var msgId = await SendEmailAsync(requester.Email, subject, body, ticket.TicketNumber, inReplyTo, references);
        await SaveOutgoingEmailAsync(spoc?.Id, requester.Id, subject, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
    }

    public async Task SendResolvedEmailAsync(Ticket ticket, Requester requester)
    {
        var template = LoadTemplate("TicketResolved");
        if (template != null)
        {
            var appName = await GetApplicationNameAsync(ticket);
            var subject = "Ticket Resolved | #" + ticket.TicketNumber;
            var body = PopulateTemplate(template, new Dictionary<string, string>
            {
                ["UserName"] = requester.FullName,
                ["TicketId"] = ticket.TicketNumber ?? ticket.Id.ToString(),
                ["ApplicationName"] = appName,
                ["Subject"] = ticket.Subject ?? "",
                ["ResolvedBy"] = ticket.AssignedToUser?.FullName ?? "Support Team",
                ["ResolvedDate"] = DateTime.Now.ToString("dd-MM-yyyy HH:mm")
            });
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, subject, body, ticket.TicketNumber, inReplyTo, references);
            await SaveOutgoingEmailAsync(null, requester.Id, subject, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
        else
        {
            var fallbackBody = $"Dear {requester.FullName},\n\nYour ticket has been marked as resolved.\n\nTicket ID: {ticket.TicketNumber}\nSubject: {ticket.Subject}\n\nThank you,\nTicket Support Team";
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, fallbackBody, fallbackBody, ticket.TicketNumber, inReplyTo, references);
            await SaveOutgoingEmailAsync(null, requester.Id, fallbackBody, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
    }

    public async Task SendClosedEmailAsync(Ticket ticket, Requester requester)
    {
        var template = LoadTemplate("TicketClosed");
        if (template != null)
        {
            var appName = await GetApplicationNameAsync(ticket);
            var subject = "Ticket Closed | #" + ticket.TicketNumber;
            var body = PopulateTemplate(template, new Dictionary<string, string>
            {
                ["UserName"] = requester.FullName,
                ["TicketId"] = ticket.TicketNumber ?? ticket.Id.ToString(),
                ["ApplicationName"] = appName,
                ["Subject"] = ticket.Subject ?? "",
                ["ClosedDate"] = DateTime.Now.ToString("dd-MM-yyyy HH:mm")
            });
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, subject, body, ticket.TicketNumber, inReplyTo, references);
            await SaveOutgoingEmailAsync(null, requester.Id, subject, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
        else
        {
            var fallbackBody = $"Dear {requester.FullName},\n\nYour ticket has been closed.\n\nTicket ID: {ticket.TicketNumber}\nSubject: {ticket.Subject}\n\nThank you,\nTicket Support Team";
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, fallbackBody, fallbackBody, ticket.TicketNumber, inReplyTo, references);
            await SaveOutgoingEmailAsync(null, requester.Id, fallbackBody, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
    }

    public async Task SendReopenedEmailAsync(Ticket ticket, Requester requester, User? spoc)
    {
        var template = LoadTemplate("TicketReopened");
        if (template != null)
        {
            var appName = await GetApplicationNameAsync(ticket);
            var subject = "Ticket Reopened | #" + ticket.TicketNumber;
            var body = PopulateTemplate(template, new Dictionary<string, string>
            {
                ["UserName"] = requester.FullName,
                ["TicketId"] = ticket.TicketNumber ?? ticket.Id.ToString(),
                ["ApplicationName"] = appName,
                ["Subject"] = ticket.Subject ?? "",
                ["ReopenedDate"] = DateTime.Now.ToString("dd-MM-yyyy HH:mm")
            });
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, subject, body, ticket.TicketNumber, inReplyTo, references);
            await SaveOutgoingEmailAsync(spoc?.Id, requester.Id, subject, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
        else
        {
            var fallbackBody = $"Dear {requester.FullName},\n\nYour ticket has been reopened and is being worked on.\n\nTicket ID: {ticket.TicketNumber}\nSubject: {ticket.Subject}\n\nThank you,\nTicket Support Team";
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, fallbackBody, fallbackBody, ticket.TicketNumber, inReplyTo, references);
            await SaveOutgoingEmailAsync(spoc?.Id, requester.Id, fallbackBody, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
    }

    public async Task SendAutoCloseEmailAsync(Ticket ticket, Requester requester)
    {
        var template = LoadTemplate("TicketClosed");
        if (template != null)
        {
            var appName = await GetApplicationNameAsync(ticket);
            var subject = "Ticket Closed | #" + ticket.TicketNumber;
            var body = PopulateTemplate(template, new Dictionary<string, string>
            {
                ["UserName"] = requester.FullName,
                ["TicketId"] = ticket.TicketNumber ?? ticket.Id.ToString(),
                ["ApplicationName"] = appName,
                ["Subject"] = ticket.Subject ?? "",
                ["ClosedDate"] = DateTime.Now.ToString("dd-MM-yyyy HH:mm")
            });
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, subject, body, ticket.TicketNumber, inReplyTo, references);
            await SaveOutgoingEmailAsync(null, requester.Id, subject, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
        else
        {
            var fallbackBody = $"Dear {requester.FullName},\n\nYour ticket has been closed due to inactivity.\n\nTicket ID: {ticket.TicketNumber}\nSubject: {ticket.Subject}\n\nThank you,\nTicket Support Team";
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(requester.Email, fallbackBody, fallbackBody, ticket.TicketNumber, inReplyTo, references);
            await SaveOutgoingEmailAsync(null, requester.Id, fallbackBody, requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
    }

    public async Task SendStatusChangeEmailAsync(Ticket ticket, string fromStatus, string toStatus)
    {
        if (ticket.Requester == null) return;

        var subject = $"Status Updated | #{ticket.TicketNumber}";
        var title = $"Dear {ticket.Requester.FullName},";
        var text = $@"<p style=""margin:0 0 16px 0;color:#333333;"">Your ticket status has been updated.</p>
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width:100%;border:1px solid #E0E0E0;border-radius:8px;margin-bottom:24px;""><tr><td style=""padding:20px 24px;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;"">Ticket ID</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#005BAC;font-weight:600;vertical-align:top;"">#{ticket.TicketNumber}</td></tr>
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;border-top:1px solid #F0F0F0;"">Subject</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#333333;vertical-align:top;border-top:1px solid #F0F0F0;"">{ticket.Subject}</td></tr>
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;border-top:1px solid #F0F0F0;"">Previous Status</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#333333;vertical-align:top;border-top:1px solid #F0F0F0;"">{fromStatus}</td></tr>
<tr><td style=""padding:6px 0;width:40%;font-size:13px;color:#666666;vertical-align:top;border-top:1px solid #F0F0F0;"">New Status</td><td style=""padding:6px 0;width:60%;font-size:14px;color:#333333;vertical-align:top;border-top:1px solid #F0F0F0;"">{toStatus}</td></tr>
</table></td></tr></table>
<p style=""margin:0 0 4px 0;color:#333333;"">Kind regards,</p>
<p style=""margin:0;color:#333333;font-weight:600;"">Issue Management System<br />Larsen &amp; Toubro Limited</p>";
        var body = WrapPlainText(title, text);
        var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
        var msgId = await SendEmailAsync(ticket.Requester.Email, subject, body, ticket.TicketNumber, inReplyTo, references);
        await SaveOutgoingEmailAsync(null, ticket.RequesterId, subject, ticket.Requester.Email, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
    }

    public async Task SendMessageNotificationAsync(Ticket ticket, TicketMessage message, string recipientEmail, string? senderName = null)
    {
        var template = LoadTemplate("TicketConversation");
        if (template != null)
        {
            var appName = await GetApplicationNameAsync(ticket);
            var subject = "New Update on Your Ticket | #" + ticket.TicketNumber;
            var (imagesHtml, inlineAttachments) = await BuildInlineImagesAsync(message.Id, ticket.Id);
            var body = PopulateTemplate(template, new Dictionary<string, string>
            {
                ["RecipientName"] = "User",
                ["TicketId"] = ticket.TicketNumber ?? ticket.Id.ToString(),
                ["ApplicationName"] = appName,
                ["Subject"] = ticket.Subject ?? "",
                ["SenderName"] = senderName ?? "System",
                ["MessageDate"] = (message.CreatedAt).ToString("dd-MM-yyyy HH:mm"),
                ["Message"] = message.Content ?? "",
                ["ImageAttachments"] = imagesHtml
            });
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(recipientEmail, subject, body, ticket.TicketNumber, inReplyTo, references, ticketMessageId: message.Id, inlineAttachments: inlineAttachments.Count > 0 ? inlineAttachments : null);
            await SaveOutgoingEmailAsync(null, ticket.RequesterId, subject, recipientEmail, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
        else
        {
            var (inReplyTo, references) = await GetThreadingInfoAsync(ticket.Id);
            var msgId = await SendEmailAsync(recipientEmail, $"Re: {ticket.Subject}", message.Content ?? "", ticket.TicketNumber, inReplyTo, references, ticketMessageId: message.Id);
            await SaveOutgoingEmailAsync(null, ticket.RequesterId, $"Re: {ticket.Subject}", recipientEmail, _systemEmail, "Sent", ticket.Id, msgId, inReplyTo, references);
        }
    }

    public async Task<(bool success, string? error)> SendReplyAsync(string toEmail, string subject, string body, string? inReplyTo = null, string? references = null)
    {
        try
        {
            var domain = GetDomain();
            var msgId = $"<{Guid.NewGuid():N}@{domain}>";
            await _outbox.EnqueueAsync(toEmail, subject, body, null,
                inReplyTo: inReplyTo, references: references, senderEmail: _systemEmail);
            _logger.LogInformation("Enqueued reply to {To} subject={Subject}", toEmail, subject);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue reply to {To}", toEmail);
            return (false, ex.Message);
        }
    }

    private async Task<string?> SendEmailAsync(string toEmail, string subject, string body, string? ticketNumber = null, string? inReplyTo = null, string? references = null, int? ticketMessageId = null, List<InlineAttachmentInfo>? inlineAttachments = null)
    {
        try
        {
            var isHtml = body.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase) ||
                         body.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase);
            var plainText = isHtml ? Regex.Replace(body, @"<[^>]+>", " ") : null;
            if (plainText != null) plainText = Regex.Replace(plainText, @"\s+", " ").Trim();

            var domain = GetDomain();
            var msgId = $"<{Guid.NewGuid():N}@{domain}>";

            await _outbox.EnqueueAsync(toEmail, subject, body, plainText,
                inReplyTo: inReplyTo, references: references, senderEmail: _systemEmail,
                ticketMessageId: ticketMessageId, inlineAttachments: inlineAttachments);

            _logger.LogInformation("Enqueued email to {To} subject={Subject}", toEmail, subject);
            return msgId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue email to {To} subject={Subject}", toEmail, subject);
            return $"<{Guid.NewGuid():N}@{GetDomain()}>";
        }
    }

    private string GetDomain()
    {
        var emailParts = _systemEmail.Split('@');
        return emailParts.Length > 1 ? emailParts[^1] : "ticketingsystem.com";
    }

    private async Task SaveOutgoingEmailAsync(int? userId, int? requesterId, string subject, string recipientEmail, string senderEmail, string status, int? ticketId = null, string? messageId = null, string? inReplyTo = null, string? references = null)
    {
        try
        {
            var emailMessage = new EmailMessage
            {
                TicketId = ticketId,
                RequesterId = requesterId,
                UserId = userId,
                MessageId = messageId ?? $"<{Guid.NewGuid():N}@{GetDomain()}>",
                InReplyTo = inReplyTo,
                References = references,
                Subject = subject,
                SenderEmail = senderEmail,
                RecipientEmail = recipientEmail,
                Direction = "Outgoing",
                Status = status,
                SentAt = DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.EmailMessages.Add(emailMessage);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save outgoing email record");
        }
    }

    private async Task<(string imagesHtml, List<InlineAttachmentInfo> inlineAttachments)> BuildInlineImagesAsync(int ticketMessageId, int ticketId)
    {
        var attachments = await _context.TicketAttachments
            .AsNoTracking()
            .Where(a => a.TicketMessageId == ticketMessageId)
            .ToListAsync();

        var imageAttachments = attachments.Where(a => a.ContentType.StartsWith("image/")).ToList();
        if (imageAttachments.Count == 0) return ("", []);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<div style=\"margin-bottom:24px;\">");
        sb.AppendLine("<p style=\"margin:0 0 10px 0;font-size:11px;text-transform:uppercase;color:#999999;letter-spacing:0.5px;\">Attachments</p>");
        sb.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"border:1px solid #E0E0E0;border-radius:8px;overflow:hidden;\">");

        var inlineAttachments = new List<InlineAttachmentInfo>();

        foreach (var att in imageAttachments)
        {
            var cid = $"img_{ticketId}_{att.Id}@ticketingsystem";
            sb.AppendLine("<tr><td style=\"padding:12px 16px;border-bottom:1px solid #F0F0F0;\">");
            sb.AppendLine($"<img src=\"cid:{cid}\" alt=\"{System.Net.WebUtility.HtmlEncode(att.FileName)}\" style=\"max-width:500px;max-height:400px;border-radius:6px;display:block;\" />");
            sb.AppendLine($"<p style=\"margin:6px 0 0 0;font-size:12px;color:#999999;\">{System.Net.WebUtility.HtmlEncode(att.FileName)} ({FormatFileSize(att.FileSize)})</p>");
            sb.AppendLine("</td></tr>");

            inlineAttachments.Add(new InlineAttachmentInfo
            {
                FileName = att.FileName,
                ContentType = att.ContentType,
                ContentId = cid,
                Data = att.FileData
            });
        }

        sb.AppendLine("</table></div>");
        return (sb.ToString(), inlineAttachments);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private static string? SanitizeMessageId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var trimmed = id.Trim();
        if (!trimmed.StartsWith('<')) trimmed = $"<{trimmed}";
        if (!trimmed.EndsWith('>')) trimmed = $"{trimmed}>";
        return trimmed;
    }

    private async Task<(string? inReplyTo, string? references)> GetThreadingInfoAsync(int ticketId)
    {
        try
        {
            var lastEmail = await _context.EmailMessages
                .Where(e => e.TicketId == ticketId)
                .OrderByDescending(e => e.SentAt ?? e.ReceivedAt)
                .FirstOrDefaultAsync();

            if (lastEmail?.MessageId == null) return (null, null);

            var sanitizedMsgId = SanitizeMessageId(lastEmail.MessageId);
            if (sanitizedMsgId == null) return (null, null);

            var allRefs = new List<string> { sanitizedMsgId };

            if (!string.IsNullOrWhiteSpace(lastEmail.References))
            {
                var parts = lastEmail.References
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => SanitizeMessageId(r))
                    .Where(r => r != null)
                    .ToList();

                allRefs.InsertRange(0, parts);
            }

            var refs = string.Join(" ", allRefs.TakeLast(5));

            return (sanitizedMsgId, refs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get threading info for ticket {TicketId}", ticketId);
            return (null, null);
        }
    }
}
