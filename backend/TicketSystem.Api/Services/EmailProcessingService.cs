using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Data.Repositories;
using TicketSystem.Api.Hubs;
using TicketSystem.Api.Models.DTOs.Ocr;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public partial class EmailProcessingService : IEmailProcessingService
{
    private readonly TicketSystemDbContext _context;
    private readonly IOcrService _ocrService;
    private readonly IEmailService _emailService;
    private readonly ITicketAssignmentService _assignmentService;
    private readonly INotificationService _notifService;
    private readonly IAuditService _auditService;
    private readonly IRequesterRepository _requesterRepo;
    private readonly ITicketRepository _ticketRepo;
    private readonly IHubContext<TicketHub> _hubContext;
    private readonly ILogger<EmailProcessingService> _logger;
    private readonly ISlaService _slaService;

    [GeneratedRegex(@"TKT-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TicketNumberRegex();

    public EmailProcessingService(
        TicketSystemDbContext context,
        IOcrService ocrService,
        IEmailService emailService,
        ITicketAssignmentService assignmentService,
        INotificationService notifService,
        IAuditService auditService,
        IRequesterRepository requesterRepo,
        ITicketRepository ticketRepo,
        IHubContext<TicketHub> hubContext,
        ILogger<EmailProcessingService> logger,
        ISlaService slaService)
    {
        _context = context;
        _ocrService = ocrService;
        _emailService = emailService;
        _assignmentService = assignmentService;
        _notifService = notifService;
        _auditService = auditService;
        _requesterRepo = requesterRepo;
        _ticketRepo = ticketRepo;
        _hubContext = hubContext;
        _logger = logger;
        _slaService = slaService;
    }

    public async Task ProcessEmailAsync(string fromAddress, string fromName, string subject, string body,
        string messageId, string? inReplyTo, string? references,
        List<EmailAttachment> attachments, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            _logger.LogWarning("Received email with no from address, skipping");
            return;
        }

        // Skip bounce-back / mailer-daemon notifications
        var skipSenders = new[] { "mailer-daemon", "postmaster", "no-reply", "noreply", "donotreply" };
        var localPart = fromAddress.Split('@')[0].ToLowerInvariant();
        if (skipSenders.Any(s => localPart == s || localPart.EndsWith("-noreply") || localPart.EndsWith("-donotreply")))
        {
            _logger.LogInformation("Skipping non-user email from {From}", fromAddress);
            return;
        }

        // 1. Check for duplicate by MessageId
        var existingEmail = await _context.EmailMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.MessageId == messageId, ct);
        if (existingEmail != null)
        {
            _logger.LogInformation("Duplicate email detected, MessageId={MessageId} already processed", messageId);
            return;
        }

        // 2. Save incoming email message first
        var emailMessage = new EmailMessage
        {
            MessageId = messageId,
            InReplyTo = inReplyTo,
            References = references,
            Subject = subject,
            SenderEmail = fromAddress,
            RecipientEmail = Environment.GetEnvironmentVariable("SMTP_USER") ?? "",
            Direction = "Incoming",
            Status = "Received",
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.EmailMessages.Add(emailMessage);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Email Received", "EmailMessage", emailMessage.Id);

        // 3. Try to find existing ticket via In-Reply-To, References, Subject
        var existingTicket = await FindExistingTicketAsync(subject, inReplyTo, references, body, ct);

        if (existingTicket != null)
        {
            _logger.LogInformation("Email belongs to existing ticket {TicketNumber}", existingTicket.TicketNumber);

            emailMessage.TicketId = existingTicket.Id;
            await _context.SaveChangesAsync(ct);

            await _auditService.LogAsync("Email Matched", "Ticket", existingTicket.Id,
                newValues: $"InReplyTo={inReplyTo}, MessageId={messageId}");

            await AppendConversationAsync(existingTicket, fromAddress, fromName, body, attachments, emailMessage, ct);

            await _auditService.LogAsync("Ticket Updated", "Ticket", existingTicket.Id, requesterId: existingTicket.RequesterId);
            return;
        }

        // 4. New ticket - run OCR extraction
        var ocrRequest = new OcrRequest
        {
            Subject = subject,
            EmailBody = body,
            Attachments = attachments.Select(a => new OcrAttachment
            {
                FileName = a.FileName,
                ContentType = a.ContentType,
                Data = a.Data
            }).ToList()
        };

        OcrExtractionResult extraction;
        try
        {
            extraction = await _ocrService.ExtractTicketInfoAsync(ocrRequest);
            await _auditService.LogAsync("OCR Extraction", "EmailMessage", emailMessage.Id, newValues: System.Text.Json.JsonSerializer.Serialize(extraction));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR extraction failed for email from {From}", fromAddress);
            extraction = new OcrExtractionResult
            {
                Subject = subject,
                Description = body,
                Application = "Unknown",
                Priority = "medium",
                Confidence = 0.0
            };
            await _auditService.LogAsync("OCR Extraction Failed", "Ticket", null, newValues: $"Error: {ex.Message}");
        }

        // 5. Find or create requester
        var requesterEmail = (!string.IsNullOrWhiteSpace(extraction.RequesterEmail)
            && !extraction.RequesterEmail.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            ? extraction.RequesterEmail
            : fromAddress;
        var requesterName = (!string.IsNullOrWhiteSpace(extraction.RequesterName)
            && !extraction.RequesterName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            ? extraction.RequesterName
            : fromName;

        var requester = await _requesterRepo.GetByEmailAsync(requesterEmail);
        if (requester == null)
        {
            requester = new Requester
            {
                Email = requesterEmail,
                FullName = requesterName,
                Company = null,
                CreatedAt = DateTime.UtcNow
            };
            requester = await _requesterRepo.AddAsync(requester);
            _logger.LogInformation("Created new requester: {Email} (Id={Id})", requesterEmail, requester.Id);

            await _auditService.LogAsync("Requester Created", "Requester", requester.Id, newValues: requesterEmail);
        }
        else
        {
            // Update name if empty
            if (string.IsNullOrWhiteSpace(requester.FullName) && !string.IsNullOrWhiteSpace(requesterName))
            {
                requester.FullName = requesterName;
                requester.UpdatedAt = DateTime.UtcNow;
                await _requesterRepo.UpdateAsync(requester);
            }
        }

        // 6. Resolve application using AI suggestion + database + confidence
        var (application, resolvedAppName) = await ResolveApplicationAsync(extraction, ct);

        // 6b. Keyword fallback: if AI returned Unknown, scan subject/body for known app names
        if (resolvedAppName == "Unknown")
        {
            var knownApps = await _context.Applications
                .AsNoTracking()
                .Where(a => a.Name != "Unknown" && a.IsActive)
                .ToListAsync(ct);

            if (knownApps.Count > 0)
            {
                var searchText = string.Join(" ",
                    new[] { extraction.Subject, extraction.Description, subject, body }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                var searchLower = searchText.ToLowerInvariant();

                var matchedApps = knownApps
                    .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                    .Where(a =>
                    {
                        var nameLower = a.Name.ToLowerInvariant();
                        return nameLower.Contains(' ')
                            ? searchLower.Contains(nameLower)
                            : Regex.IsMatch(searchLower, $@"\b{Regex.Escape(nameLower)}\b");
                    })
                    .ToList();

                if (matchedApps.Count == 1)
                {
                    application = matchedApps[0];
                    resolvedAppName = application.Name;
                    _logger.LogInformation("Keyword fallback matched application '{AppName}' from email text", resolvedAppName);
                }
                else if (matchedApps.Count > 1)
                {
                    _logger.LogWarning("Keyword fallback matched multiple applications: {Apps}. Keeping Unknown.",
                        string.Join(", ", matchedApps.Select(a => a.Name)));
                }
            }
        }

        // 7. Determine SPOC (only for known applications)
        var spocUserId = (application != null && resolvedAppName != "Unknown")
            ? await _assignmentService.GetPrimarySpocUserIdAsync(application.Id)
            : null;

        // 8. Get In Progress status ID (Id=1)
        const int inProgressStatusId = 1;

        // 9. Create ticket
        var seq = await GetNextSequenceAsync(ct);
        var ticketNumber = $"TKT-{seq}";

        var unknownAppId = await GetUnknownApplicationIdAsync(ct);
        var ticket = new Ticket
        {
            TicketNumber = ticketNumber,
            RequesterId = requester.Id,
            ApplicationId = application?.Id ?? unknownAppId,
            AssignedToUserId = spocUserId,
            StatusId = inProgressStatusId,
            Subject = !string.IsNullOrWhiteSpace(extraction.Subject) ? extraction.Subject : subject,
            Description = !string.IsNullOrWhiteSpace(extraction.Description) ? extraction.Description : body,
            Priority = extraction.Priority ?? "medium",
            SlaDeadline = DateTime.UtcNow.AddHours(4),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created ticket {TicketNumber} for requester {RequesterEmail}", ticketNumber, requesterEmail);

        var emailCreatedTicket = await _ticketRepo.GetTicketListResponseAsync(ticket.Id);
        if (emailCreatedTicket != null)
            await _hubContext.Clients.All.SendAsync("TicketCreated", emailCreatedTicket, ct);

        // 10. Create first conversation
        var messageContent = body;
        if (!string.IsNullOrWhiteSpace(extraction.AttachmentsSummary))
        {
            messageContent += $"\n\n--- Attachments Summary ---\n{extraction.AttachmentsSummary}";
        }

        var ticketMessage = new TicketMessage
        {
            TicketId = ticket.Id,
            RequesterId = requester.Id,
            MessageSourceType = "Requester",
            Content = messageContent,
            IsInternal = false,
            InReplyTo = inReplyTo,
            References = references,
            CreatedAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(ticketMessage);
        await _context.SaveChangesAsync(ct);

        // 11. Store attachments
        foreach (var attachment in attachments)
        {
            try
            {
                // Dedup: skip if file with same name and size already exists for this message
                var alreadyExists = await _context.TicketAttachments
                    .AnyAsync(a => a.TicketMessageId == ticketMessage.Id
                                && a.FileName == attachment.FileName
                                && a.FileSize == attachment.Data.Length, ct);
                if (alreadyExists) continue;

                var ticketAttachment = new TicketAttachment
                {
                    TicketMessageId = ticketMessage.Id,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    FileSize = attachment.Data.Length,
                    FileData = attachment.Data,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TicketAttachments.Add(ticketAttachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store attachment {FileName} for ticket {TicketNumber}", attachment.FileName, ticketNumber);
            }
        }
        await _context.SaveChangesAsync(ct);

        // 12. Update email message with ticket info
        emailMessage.TicketId = ticket.Id;
        emailMessage.RequesterId = requester.Id;
        await _context.SaveChangesAsync(ct);

        // 13. Create status history
        var statusHistory = new TicketStatusHistory
        {
            TicketId = ticket.Id,
            FromStatusId = null,
            ToStatusId = inProgressStatusId,
            ChangedByRequesterId = requester.Id,
            Remarks = "Ticket created via email",
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketStatusHistories.Add(statusHistory);
        await _context.SaveChangesAsync(ct);

        // 13b. Start SLA for the new ticket
        try
        {
            await _slaService.StartSlaAsync(ticket.Id, ticket.Priority);
            _logger.LogInformation("SLA started for ticket {TicketNumber} with priority {Priority}", ticketNumber, ticket.Priority);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SLA for ticket {TicketNumber}", ticketNumber);
        }

        // 14. Notify SPOC
        if (spocUserId.HasValue)
        {
            try
            {
                await _notifService.CreateAsync(
                    spocUserId.Value,
                    ticket.Id,
                    "ticket_created",
                    $"New Ticket: {ticketNumber}",
                    $"New ticket from {requester.FullName}: {ticket.Subject}"
                );

                var spocUser = await _context.Users.FindAsync(spocUserId.Value);
                var spocEmail = spocUser?.Email;
                await _emailService.SendTicketCreatedEmailAsync(ticket, requester, spocEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify SPOC for ticket {TicketNumber}", ticketNumber);
            }
        }

        // Notify all active admin users about the new ticket
        try
        {
            var adminIds = await _context.Users.AsNoTracking()
                .Where(u => u.IsActive && u.RoleId == 1)
                .Select(u => u.Id)
                .ToListAsync(ct);
            foreach (var adminId in adminIds)
            {
                await _notifService.CreateAsync(adminId, ticket.Id, "ticket_created",
                    $"New Ticket: {ticketNumber}",
                    $"New ticket from {requester.FullName}: {ticket.Subject}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify admin users for ticket {TicketNumber}", ticketNumber);
        }

        // 15. Audit log
        await _auditService.LogAsync("Ticket Created", "Ticket", ticket.Id,
            newValues: System.Text.Json.JsonSerializer.Serialize(new { ticket.TicketNumber, ticket.Subject, ticket.Priority, Application = application?.Name }));

        _logger.LogInformation("Email processing complete for ticket {TicketNumber}", ticketNumber);
    }

    private async Task<Ticket?> FindExistingTicketAsync(string subject, string? inReplyTo, string? references, string body, CancellationToken ct)
    {
        // Check by In-Reply-To header - look for MessageId match
        if (!string.IsNullOrWhiteSpace(inReplyTo))
        {
            var emailByReply = await _context.EmailMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.MessageId == inReplyTo && e.TicketId.HasValue, ct);
            if (emailByReply?.TicketId.HasValue == true)
            {
                var ticket = await _context.Tickets
                    .Include(t => t.Requester)
                    .FirstOrDefaultAsync(t => t.Id == emailByReply.TicketId.Value, ct);
                if (ticket != null) return ticket;
            }
        }

        // Check by References header
        if (!string.IsNullOrWhiteSpace(references))
        {
            var refIds = references.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var refId in refIds)
            {
                var emailByRef = await _context.EmailMessages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.MessageId == refId && e.TicketId.HasValue, ct);
                if (emailByRef?.TicketId.HasValue == true)
                {
                    var ticket = await _context.Tickets
                        .Include(t => t.Requester)
                        .FirstOrDefaultAsync(t => t.Id == emailByRef.TicketId.Value, ct);
                    if (ticket != null) return ticket;
                }
            }
        }

        // Check by Ticket Number in subject
        var match = TicketNumberRegex().Match(subject);
        if (match.Success)
        {
            var ticketNum = $"TKT-{match.Groups[1].Value}";
            var ticketBySubject = await _context.Tickets
                .Include(t => t.Requester)
                .FirstOrDefaultAsync(t => t.TicketNumber == ticketNum, ct);
            if (ticketBySubject != null) return ticketBySubject;
        }

        return null;
    }

    private static string StripReplyQuotes(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";

        var lines = body.Split('\n');
        var cleanLines = new List<string>();
        bool inQuote = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Outlook original message separator
            if (line.StartsWith("-----Original Message-----", StringComparison.OrdinalIgnoreCase))
                break;

            // Outlook alternative separator
            if (line.StartsWith("________________________________________"))
                break;

            // Gmail/standard: "On <date>, <name> wrote:"
            if (Regex.IsMatch(line, @"^On\s+.+wrote:\s*$", RegexOptions.IgnoreCase))
            {
                inQuote = true;
                continue;
            }

            // Generic: "<date>, <name> wrote:" 
            if (Regex.IsMatch(line, @"\w+,\s+\d{1,2}\s+\w+\s+\d{4}\s+at\s+\d{1,2}:\d{2}", RegexOptions.IgnoreCase) &&
                line.Contains("wrote:", StringComparison.OrdinalIgnoreCase))
            {
                inQuote = true;
                continue;
            }

            // "From: <name>" line in forwarded/Outlook replies (signals start of quoted)
            if (Regex.IsMatch(line, @"^From:\s+.+", RegexOptions.IgnoreCase) &&
                cleanLines.Count > 0 &&
                string.IsNullOrWhiteSpace(cleanLines[^1]))
            {
                inQuote = true;
                continue;
            }

            // "Sent: <date>" typically follows "From:" in Outlook
            if (inQuote && Regex.IsMatch(line, @"^Sent:\s+.+", RegexOptions.IgnoreCase))
                continue;

            // "To: <email>" in quoted block
            if (inQuote && Regex.IsMatch(line, @"^To:\s+.+", RegexOptions.IgnoreCase))
                continue;

            // "Subject: " in quoted block (forward/reply headers)
            if (inQuote && Regex.IsMatch(line, @"^Subject:\s+.+", RegexOptions.IgnoreCase))
                continue;

            // "Cc: " in quoted block
            if (inQuote && Regex.IsMatch(line, @"^Cc:\s+.+", RegexOptions.IgnoreCase))
                continue;

            // "Date: " in quoted block
            if (inQuote && Regex.IsMatch(line, @"^Date:\s+.+", RegexOptions.IgnoreCase))
                continue;

            // Lines starting with '>' (email quote marker)
            if (line.TrimStart().StartsWith('>'))
            {
                inQuote = true;
                continue;
            }

            // Signature separator: "-- " (dash dash space) per RFC 3676
            if (line.Trim().Equals("-- ", StringComparison.Ordinal) ||
                line.Trim().Equals("--", StringComparison.Ordinal))
                break;

            // Common sign-offs that typically precede a signature
            var trimmedLower = line.Trim().ToLowerInvariant();
            if (trimmedLower is "regards," or "regards" or "thanks," or "thanks" or "thank you," or "thank you" or
                "best regards," or "best regards" or "kind regards," or "kind regards" or "warm regards," or "warm regards" or
                "sincerely," or "sincerely" or "yours sincerely," or "yours sincerely" or "cheers," or "cheers" or
                "many thanks," or "many thanks" or "with thanks," or "with thanks")
            {
                // If the line after this looks like a signature (short, no typical sentence), treat sign-off + rest as sig
                break;
            }

            // Mobile/email client signatures
            if (trimmedLower.StartsWith("sent from") && (
                trimmedLower.Contains("iphone") || trimmedLower.Contains("android") ||
                trimmedLower.Contains("mobile") || trimmedLower.Contains("mail for") ||
                trimmedLower.Contains("outlook") || trimmedLower.Contains("galaxy") ||
                trimmedLower.Contains("ipad") || trimmedLower.Contains("tablet") ||
                trimmedLower.Contains("blackberry") || trimmedLower.Contains("samsung")))
                break;

            // Email footer / disclaimer starts
            if (trimmedLower.StartsWith("this email") || trimmedLower.StartsWith("this message") ||
                trimmedLower.StartsWith("disclaimer") || trimmedLower.StartsWith("confidentiality") ||
                trimmedLower.StartsWith("the information") || trimmedLower.StartsWith("please consider") ||
                trimmedLower.StartsWith("this communication") || trimmedLower.StartsWith("this e-mail") ||
                trimmedLower.StartsWith("the contents") || trimmedLower.StartsWith("virus") ||
                trimmedLower.StartsWith("________________________________________________________________") ||
                trimmedLower.StartsWith("please note"))
                break;

            // "On <date>, at <time>, <name> <email> wrote:" (Outlook RTF format)
            if (Regex.IsMatch(line, @"^On\s+\d{1,2}[-/]\d{1,2}[-/]\d{2,4}", RegexOptions.IgnoreCase) &&
                line.Contains("wrote:", StringComparison.OrdinalIgnoreCase))
            {
                inQuote = true;
                continue;
            }

            // "---" separator lines often used before forwarded content
            if (line.Trim().Equals("---", StringComparison.Ordinal) ||
                line.Trim().Equals("-----", StringComparison.Ordinal))
            {
                var nextIsForward = lines.Length > Array.IndexOf(lines, rawLine) + 1 &&
                    Regex.IsMatch(lines[Array.IndexOf(lines, rawLine) + 1].Trim(), @"^From:\s+", RegexOptions.IgnoreCase);
                if (nextIsForward)
                {
                    if (!cleanLines.Any(l => !string.IsNullOrWhiteSpace(l)))
                        break;
                    inQuote = true;
                    continue;
                }
                // Single separator line is ambiguous, keep it
            }

            // If we're past a quoted block and see a blank line, check if we're transitioning
            if (inQuote && string.IsNullOrWhiteSpace(line))
            {
                // Multiple blank lines after quote could still be within quoted section
                continue;
            }

            if (!inQuote)
            {
                cleanLines.Add(line);
            }
        }

        // Trim trailing blank lines
        while (cleanLines.Count > 0 && string.IsNullOrWhiteSpace(cleanLines[^1]))
            cleanLines.RemoveAt(cleanLines.Count - 1);

        // Trim leading blank lines
        while (cleanLines.Count > 0 && string.IsNullOrWhiteSpace(cleanLines[0]))
            cleanLines.RemoveAt(0);

        return string.Join("\n", cleanLines).Trim();
    }

    private async Task AppendConversationAsync(Ticket ticket, string fromAddress, string fromName, string body,
        List<EmailAttachment> attachments, EmailMessage emailMessage, CancellationToken ct)
    {
        var requester = await _context.Requesters
            .FirstOrDefaultAsync(r => r.Email == fromAddress, ct);

        // Create requester if not found (consistent with new ticket creation path)
        if (requester == null)
        {
            requester = new Requester
            {
                Email = fromAddress,
                FullName = fromName,
                Company = null,
                CreatedAt = DateTime.UtcNow
            };
            _context.Requesters.Add(requester);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Created new requester from reply email: {Email} (Id={Id})", fromAddress, requester.Id);
        }

        var cleanedBody = StripReplyQuotes(body);

        if (string.IsNullOrWhiteSpace(cleanedBody))
        {
            _logger.LogInformation("Email reply from {From} on ticket {TicketNumber} contained only quoted content. Skipping message creation.",
                fromAddress, ticket.TicketNumber);
            return;
        }

        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            RequesterId = requester.Id,
            MessageSourceType = "Requester",
            Content = cleanedBody,
            IsInternal = false,
            InReplyTo = emailMessage.InReplyTo,
            References = emailMessage.References,
            CreatedAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(message);
        await _context.SaveChangesAsync(ct);

        foreach (var attachment in attachments)
        {
            try
            {
                // Dedup: skip if file with same name and size already exists for this message
                var alreadyExists = await _context.TicketAttachments
                    .AnyAsync(a => a.TicketMessageId == message.Id
                                && a.FileName == attachment.FileName
                                && a.FileSize == attachment.Data.Length, ct);
                if (alreadyExists) continue;

                var ticketAttachment = new TicketAttachment
                {
                    TicketMessageId = message.Id,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    FileSize = attachment.Data.Length,
                    FileData = attachment.Data,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TicketAttachments.Add(ticketAttachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store attachment {FileName} for existing ticket {TicketNumber}",
                    attachment.FileName, ticket.TicketNumber);
            }
        }
        await _context.SaveChangesAsync(ct);

        ticket.UpdatedAt = DateTime.UtcNow;
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync(ct);

        // Resume SLA on email reply
        await _slaService.ResumeSlaAsync(ticket.Id);

        // Auto-transition from Waiting to In Progress when a reply is added
        if (ticket.StatusId == 2) // Waiting
        {
            ticket.StatusId = 1; // In Progress
            ticket.UpdatedAt = DateTime.UtcNow;
            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync(ct);

            // Add status history
            var history = new TicketStatusHistory
            {
                TicketId = ticket.Id,
                FromStatusId = 2,
                ToStatusId = 1,
                ChangedByRequesterId = requester?.Id,
                Remarks = "Auto-transitioned from Waiting to In Progress on email reply",
                CreatedAt = DateTime.UtcNow
            };
            _context.TicketStatusHistories.Add(history);
            await _context.SaveChangesAsync(ct);
        }

        // Notify assigned SPOC
        if (ticket.AssignedToUserId.HasValue)
        {
            try
            {
                var preview = cleanedBody.Length <= 100 ? cleanedBody : cleanedBody[..100] + "...";
                await _notifService.CreateAsync(
                    ticket.AssignedToUserId.Value,
                    ticket.Id,
                    "new_message",
                    $"New reply on {ticket.TicketNumber}",
                    $"{fromName} replied: {preview}"
                );

                var spocUser = await _context.Users.FindAsync(ticket.AssignedToUserId.Value);
                if (spocUser != null)
                {
                    await _emailService.SendMessageNotificationAsync(ticket, message, spocUser.Email, fromName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify SPOC for new message on ticket {TicketNumber}", ticket.TicketNumber);
            }
        }

        var appendUpdated = await _ticketRepo.GetTicketListResponseAsync(ticket.Id);
        if (appendUpdated != null)
            await _hubContext.Clients.All.SendAsync("TicketUpdated", appendUpdated);

        _logger.LogInformation("Appended conversation to ticket {TicketNumber}", ticket.TicketNumber);
    }

    private async Task<(Application? Entity, string ResolvedName)> ResolveApplicationAsync(OcrExtractionResult extraction, CancellationToken ct)
    {
        var aiAppName = (extraction.Application ?? "").Trim();
        var confidence = extraction.Confidence;

        // Normalize: collapse whitespace, strip minor punctuation
        var normalized = NormalizeAppName(aiAppName);

        _logger.LogInformation("Resolving application: AI suggested '{AppName}' (normalized: '{Normalized}') with confidence {Confidence}",
            aiAppName, normalized, confidence);

        // Edge case: empty, unknown, or nonsense
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("AI application '{AppName}' is empty or Unknown. Using Unknown.", aiAppName);
            var unknown = await _context.Applications
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Name == "Unknown", ct);
            return (unknown, "Unknown");
        }

        // Search database for a matching application (case-insensitive, normalized)
        var allApps = await _context.Applications
            .AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync(ct);

        // Find all matches using normalized comparison
        var matches = allApps
            .Where(a => NormalizeAppName(a.Name) == normalized)
            .ToList();

        // If no exact-normalized match, try contains (to handle minor differences)
        if (matches.Count == 0)
        {
            matches = allApps
                .Where(a => NormalizeAppName(a.Name).Contains(normalized) ||
                            normalized.Contains(NormalizeAppName(a.Name)))
                .ToList();
        }

        // If multiple known applications match, leave as Unknown for manual selection
        if (matches.Count > 1)
        {
            var matchNames = string.Join(", ", matches.Select(m => m.Name));
            _logger.LogWarning("AI application '{AppName}' matched multiple applications: {Matches}. Leaving as Unknown for manual selection.",
                aiAppName, matchNames);
            var unknown = await _context.Applications
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Name == "Unknown", ct);
            return (unknown, "Unknown");
        }

        // Exact database match found - use it regardless of confidence
        if (matches.Count == 1)
        {
            _logger.LogInformation("AI application '{AppName}' matched database application '{DbApp}' (confidence: {Confidence}). Using database match.",
                aiAppName, matches[0].Name, confidence);
            return (matches[0], matches[0].Name);
        }

        // No database match found
        // Use AI result only if confidence is >= 0.70
        if (confidence >= 0.70)
        {
            _logger.LogInformation("AI application '{AppName}' not found in database but confidence ({Confidence}) >= 0.70. Using AI suggestion.",
                aiAppName, confidence);
            var unknown = await _context.Applications
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Name == "Unknown", ct);
            return (unknown, aiAppName);
        }

        // Low confidence and no database match - fall back to Unknown
        _logger.LogWarning("AI application '{AppName}' not found in database and confidence ({Confidence}) < 0.70. Falling back to Unknown.",
            aiAppName, confidence);
        var unknownApp = await _context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == "Unknown", ct);
        return (unknownApp, "Unknown");
    }

    private static string NormalizeAppName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var result = name.Trim().ToLowerInvariant();
        // Collapse whitespace
        while (result.Contains("  ")) result = result.Replace("  ", " ");
        // Strip common minor punctuation
        result = result.Trim('.', ',', ';', ':', '!', '?', '-', '_', '/', '\\', '\'', '"');
        return result;
    }

    private async Task<int> GetNextSequenceAsync(CancellationToken ct)
    {
        var maxTicketNumbers = await _context.Tickets
            .Where(t => t.TicketNumber.StartsWith("TKT-"))
            .Select(t => t.TicketNumber)
            .ToListAsync(ct);

        var maxNum = 0;
        foreach (var tn in maxTicketNumbers)
        {
            if (int.TryParse(tn.AsSpan(4), out var num) && num > maxNum)
                maxNum = num;
        }

        return maxNum + 1;
    }

    private async Task<int> GetUnknownApplicationIdAsync(CancellationToken ct)
    {
        var unknown = await _context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == "Unknown", ct);
        return unknown?.Id ?? 1;
    }
}
