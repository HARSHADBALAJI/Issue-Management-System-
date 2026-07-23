using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Data.Repositories;
using TicketSystem.Api.Hubs;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Tickets;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepo;
    private readonly IRequesterRepository _requesterRepo;
    private readonly IUserRepository _userRepo;
    private readonly IApplicationRepository _appRepo;
    private readonly INotificationService _notifService;
    private readonly IEmailService _emailService;
    private readonly ITicketAssignmentService _assignmentService;
    private readonly IAuditService _auditService;
    private readonly IHubContext<TicketHub> _hubContext;
    private readonly TicketSystemDbContext _context;
    private readonly ISlaService _slaService;

    public TicketService(ITicketRepository ticketRepo, IRequesterRepository requesterRepo,
        IUserRepository userRepo, IApplicationRepository appRepo, INotificationService notifService,
        IEmailService emailService, ITicketAssignmentService assignmentService, IAuditService auditService,
        IHubContext<TicketHub> hubContext, TicketSystemDbContext context,
        ISlaService slaService)
    {
        _ticketRepo = ticketRepo;
        _requesterRepo = requesterRepo;
        _userRepo = userRepo;
        _appRepo = appRepo;
        _notifService = notifService;
        _emailService = emailService;
        _assignmentService = assignmentService;
        _auditService = auditService;
        _hubContext = hubContext;
        _context = context;
        _slaService = slaService;
    }

    public async Task<PagedResponse<TicketListResponse>> GetPagedAsync(TicketQueryParams query)
        => await _ticketRepo.GetPagedAsync(query);

    public async Task<TicketDetailResponse?> GetByIdAsync(int id)
        => await _ticketRepo.GetDetailAsync(id);

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request)
    {
        var requester = await _requesterRepo.GetByIdAsync(request.RequesterId)
            ?? throw new KeyNotFoundException("Requester not found");

        var seq = await _ticketRepo.GetNextSequenceAsync();
        var ticketNumber = $"TKT-{seq:D4}";

        var entity = new Ticket
        {
            TicketNumber = ticketNumber,
            RequesterId = request.RequesterId,
            ApplicationId = request.ApplicationId,
            Subject = request.Subject,
            Description = request.Description,
            Priority = request.Priority,
            StatusId = 5, // open
            SlaDeadline = DateTime.UtcNow.AddHours(4)
        };

        await _ticketRepo.AddAsync(entity);

        // Create the first conversation message as the single source of truth
        var firstMessage = new TicketMessage
        {
            TicketId = entity.Id,
            RequesterId = request.RequesterId,
            MessageSourceType = "Requester",
            Content = request.Description,
            IsInternal = false,
            CreatedAt = entity.CreatedAt
        };
        await _ticketRepo.AddMessageAsync(firstMessage);

        // Record initial status history
        var statusHistory = new TicketStatusHistory
        {
            TicketId = entity.Id,
            FromStatusId = null,
            ToStatusId = 5,
            ChangedByRequesterId = request.RequesterId,
            Remarks = "Ticket created",
            CreatedAt = entity.CreatedAt
        };
        await _ticketRepo.AddStatusHistoryAsync(statusHistory);

        // Start SLA for the new ticket
        await _slaService.StartSlaAsync(entity.Id, entity.Priority ?? "medium");

        // Load application for response
        var app = await _appRepo.GetByIdAsync(request.ApplicationId);
        var appName = app?.Name ?? "";
        var appAlias = string.IsNullOrWhiteSpace(appName) ? ""
            : (appName.Length > 10 ? appName[..10].ToUpperInvariant() : appName.ToUpperInvariant()).Replace(" ", "");

        var listResponse = await _ticketRepo.GetTicketListResponseAsync(entity.Id);
        if (listResponse != null)
            await _hubContext.Clients.All.SendAsync("TicketCreated", listResponse);

        // Notify all active admin users about the new ticket
        try
        {
            var adminUsers = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.RoleId == 1)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var adminId in adminUsers)
            {
                await _notifService.CreateAsync(adminId, entity.Id, "ticket_created",
                    $"New Ticket: {entity.TicketNumber}",
                    $"{requester.FullName} created ticket {entity.TicketNumber}: {entity.Subject}");
            }
        }
        catch { }

        return new TicketResponse
        {
            Id = entity.Id,
            TicketNumber = entity.TicketNumber,
            RequesterId = entity.RequesterId,
            RequesterName = requester.FullName,
            RequesterEmail = requester.Email,
            ApplicationId = entity.ApplicationId,
            ApplicationName = appName,
            ApplicationAlias = appAlias,
            StatusId = 5,
            StatusName = "open",
            StatusDisplayName = "Open",
            Subject = entity.Subject,
            Description = entity.Description,
            Priority = entity.Priority,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<TicketMessageResponse> AddMessageAsync(int ticketId, CreateTicketMessageRequest request, int userId, List<IFormFile>? files = null)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        var now = DateTime.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync();

        TicketMessage message;
        List<TicketAttachment> attachments;

        try
        {
            message = new TicketMessage
            {
                TicketId = ticketId,
                Content = request.Content,
                IsInternal = request.IsInternal,
                UserId = userId,
                MessageSourceType = "User",
                CreatedAt = now
            };

            message = await _ticketRepo.AddMessageAsync(message);

            // Process uploaded files
            attachments = await ProcessUploadedFilesAsync(message.Id, files ?? request.Files, userId);
            message.Attachments = attachments;

            ticket.UpdatedAt = now;
            await _ticketRepo.UpdateAsync(ticket);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        // Resume SLA on user reply
        await _slaService.ResumeSlaAsync(ticketId);

        // Auto-transition from Waiting to In Progress when a reply is added
        if (ticket.StatusId == 2) // Waiting
        {
            ticket.StatusId = 1; // In Progress
            ticket.UpdatedAt = DateTime.UtcNow;
            await _ticketRepo.UpdateAsync(ticket);
            
            // Add status history
            var history = new TicketStatusHistory
            {
                TicketId = ticketId,
                FromStatusId = 2,
                ToStatusId = 1,
                ChangedByUserId = userId,
                Remarks = "Auto-transitioned from Waiting to In Progress on new reply",
                CreatedAt = DateTime.UtcNow
            };
            await _ticketRepo.AddStatusHistoryAsync(history);
        }

        if (!request.IsInternal && ticket.AssignedToUserId.HasValue)
        {
            var fullTicket = await _ticketRepo.GetDetailAsync(ticketId);
            var spocUser = await _userRepo.GetByIdAsync(ticket.AssignedToUserId.Value);

            if (fullTicket != null && spocUser != null)
            {
                var requester = await _requesterRepo.GetByIdAsync(ticket.RequesterId);
                if (requester != null)
                {
                    await _emailService.SendSpocReplyEmailAsync(ticket, message, requester, spocUser);

                    await _notifService.CreateAsync(
                        ticket.AssignedToUserId.Value,
                        ticketId,
                        "spoc_reply",
                        $"New reply on {ticket.TicketNumber}",
                        message.Content);
                }
            }
        }

        var updatedTicket = await _ticketRepo.GetTicketListResponseAsync(ticketId);
        if (updatedTicket != null)
            await _hubContext.Clients.All.SendAsync("TicketUpdated", updatedTicket);

        var user = await _userRepo.GetByIdAsync(userId);

        return new TicketMessageResponse
        {
            Id = message.Id,
            TicketId = ticketId,
            Content = message.Content,
            IsInternal = message.IsInternal,
            MessageSourceType = message.MessageSourceType,
            UserId = userId,
            UserName = user?.FullName,
            UserRole = user?.Role?.Name,
            CreatedAt = message.CreatedAt,
            Attachments = attachments.Select(a => new TicketMessageResponse.AttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileSize = a.FileSize,
                CreatedAt = a.CreatedAt
            }).ToList()
        };
    }

    private async Task<List<TicketAttachment>> ProcessUploadedFilesAsync(int messageId, List<IFormFile>? files, int userId)
    {
        var attachments = new List<TicketAttachment>();
        if (files == null || files.Count == 0) return attachments;

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var data = memoryStream.ToArray();

            // Dedup: skip if file with same name and size already exists for this message
            var alreadyExists = await _context.TicketAttachments
                .AnyAsync(a => a.TicketMessageId == messageId
                            && a.FileName == file.FileName
                            && a.FileSize == file.Length);
            if (alreadyExists) continue;

            var attachment = new TicketAttachment
            {
                TicketMessageId = messageId,
                FileName = file.FileName,
                ContentType = file.ContentType ?? "application/octet-stream",
                FileSize = file.Length,
                FileData = data,
                CreatedAt = DateTime.UtcNow
            };

            _context.TicketAttachments.Add(attachment);
            attachments.Add(attachment);
        }

        await _context.SaveChangesAsync();
        return attachments;
    }

    public async Task<CorrectiveActionResponse> AddCorrectiveActionAsync(int ticketId, CreateCorrectiveActionRequest request, int userId)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        var now = DateTime.UtcNow;

        TicketCorrectiveAction action;

        await using (var tx = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                action = new TicketCorrectiveAction
                {
                    TicketId = ticketId,
                    Description = request.Description,
                    PerformedAt = request.PerformedAt == default ? now : request.PerformedAt,
                    PerformedByUserId = userId
                };

                action = await _ticketRepo.AddCorrectiveActionAsync(action);

                ticket.UpdatedAt = now;
                await _ticketRepo.UpdateAsync(ticket);

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // Audit log
        await _auditService.LogAsync("Corrective Action Added", "Ticket", ticketId,
            userId: userId, newValues: request.Description);

        // Notify assigned SPOC
        if (ticket.AssignedToUserId.HasValue)
        {
            await _notifService.CreateAsync(
                ticket.AssignedToUserId.Value,
                ticketId,
                "corrective_action",
                $"Corrective action added on {ticket.TicketNumber}",
                action.Description);

            var spocUser = await _userRepo.GetByIdAsync(ticket.AssignedToUserId.Value);
            if (spocUser != null)
                await _emailService.SendReplyAsync(spocUser.Email,
                    $"Corrective Action | #{ticket.TicketNumber}",
                    $"A corrective action has been added to ticket {ticket.TicketNumber}.\n\nAction: {action.Description}\nSubject: {ticket.Subject}");
        }

        // Broadcast update via SignalR
        var caUpdated = await _ticketRepo.GetTicketListResponseAsync(ticketId);
        if (caUpdated != null)
            await _hubContext.Clients.All.SendAsync("TicketUpdated", caUpdated);

        return new CorrectiveActionResponse
        {
            Id = action.Id,
            TicketId = ticketId,
            Description = action.Description,
            PerformedAt = action.PerformedAt,
            CreatedAt = action.CreatedAt
        };
    }

    public async Task AssignAsync(int ticketId, AssignTicketRequest request, int userId)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        var user = await _userRepo.GetByIdAsync(request.AssignedToUserId)
            ?? throw new KeyNotFoundException("User not found");

        if (ticket.AssignedToUserId == request.AssignedToUserId)
            throw new InvalidOperationException($"Ticket is already assigned to {user.FullName}");

        var previousSpocId = ticket.AssignedToUserId;
        var previousSpocName = previousSpocId.HasValue
            ? (await _userRepo.GetByIdAsync(previousSpocId.Value))?.FullName
            : null;

        var assignedByUser = await _userRepo.GetByIdAsync(userId);
        var now = DateTime.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync();

        try
        {
            ticket.AssignedToUserId = request.AssignedToUserId;
            ticket.UpdatedAt = now;
            await _ticketRepo.UpdateAsync(ticket);

            var assignHistory = new TicketStatusHistory
            {
                TicketId = ticketId,
                FromStatusId = ticket.StatusId,
                ToStatusId = ticket.StatusId,
                ChangedByUserId = userId,
                Remarks = previousSpocId.HasValue
                    ? $"Reassigned from {previousSpocName} to {user.FullName} by {assignedByUser?.FullName ?? "System"}"
                    : $"Assigned to {user.FullName} by {assignedByUser?.FullName ?? "System"}",
                CreatedAt = now
            };
            await _ticketRepo.AddStatusHistoryAsync(assignHistory);

            var requester = await _requesterRepo.GetByIdAsync(ticket.RequesterId);
            if (requester != null)
            {
                if (previousSpocId.HasValue)
                {
                    await _emailService.SendReassignedEmailAsync(ticket, requester, user);
                }
                else
                {
                    await _emailService.SendAssignedEmailAsync(ticket, requester, user);
                }

                // Notification to new SPOC
                await _notifService.CreateAsync(
                    request.AssignedToUserId,
                    ticketId,
                    "ticket_assigned",
                    $"Ticket {ticket.TicketNumber} assigned to you",
                    $"Subject: {ticket.Subject}\nRequester: {requester.FullName}");

                // Email to new SPOC
                await _emailService.SendAssignedToSpocEmailAsync(ticket, user);

                // Notify previous SPOC if reassigned
                if (previousSpocId.HasValue)
                {
                    await _notifService.CreateAsync(
                        previousSpocId.Value,
                        ticketId,
                        "ticket_unassigned",
                        $"Ticket {ticket.TicketNumber} unassigned from you",
                        $"Ticket reassigned to {user.FullName}");
                }
            }

            await _auditService.LogAsync(
                previousSpocId.HasValue ? "Ticket Reassigned" : "Ticket Assigned",
                "Ticket", ticketId,
                userId: userId,
                oldValues: previousSpocName,
                newValues: user.FullName);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var assignUpdated = await _ticketRepo.GetTicketListResponseAsync(ticketId);
        if (assignUpdated != null)
            await _hubContext.Clients.All.SendAsync("TicketUpdated", assignUpdated);
    }

    private static readonly Dictionary<int, int[]> AllowedStatusTransitions = new()
    {
        [5] = [1, 2, 3],     // open -> in_progress, waiting, resolved
        [1] = [2, 3, 5],     // in_progress -> waiting, resolved, open
        [2] = [1, 3, 5],     // waiting -> in_progress, resolved, open
        [3] = [4, 1, 5],     // resolved -> closed, in_progress, open
        [4] = [],             // closed -> (only via ReopenAsync)
    };

    public async Task UpdateStatusAsync(int ticketId, UpdateTicketStatusRequest request, int userId)
    {
        var ticket = await _ticketRepo.GetByIdWithIncludesAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        if (ticket.StatusId == request.StatusId)
            throw new InvalidOperationException($"Ticket is already in '{ticket.Status.Name}' status");

        if (!AllowedStatusTransitions.TryGetValue(ticket.StatusId, out var allowed) || !allowed.Contains(request.StatusId))
            throw new InvalidOperationException($"Cannot change status from '{ticket.Status.Name}' to the requested status");

        var fromStatusId = ticket.StatusId;
        var fromStatusName = ticket.Status.Name;
        var toStatusName = (await _context.TicketStatuses.FindAsync(request.StatusId))?.Name ?? "unknown";
        var now = DateTime.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync();

        try
        {
            ticket.StatusId = request.StatusId;
            ticket.UpdatedAt = now;
            if (request.StatusId == 3) ticket.ResolvedAt = now;
            if (request.StatusId == 4) ticket.ClosedAt = now;
            if (request.StatusId != 3 && request.StatusId != 4) ticket.ResolvedAt = null;
            await _ticketRepo.UpdateAsync(ticket);

            var history = new TicketStatusHistory
            {
                TicketId = ticketId,
                FromStatusId = fromStatusId,
                ToStatusId = request.StatusId,
                ChangedByUserId = userId,
                Remarks = request.Remarks ?? $"Status changed from {fromStatusName} to {toStatusName}",
                CreatedAt = now
            };

            await _ticketRepo.AddStatusHistoryAsync(history);

            // SLA tracking
            if (request.StatusId == 2)
                await _slaService.PauseSlaAsync(ticketId);
            else if (request.StatusId == 3)
                await _slaService.CompleteSlaAsync(ticketId);
            else if (request.StatusId == 1)
                await _slaService.ResumeSlaAsync(ticketId);

            var requester = await _requesterRepo.GetByIdAsync(ticket.RequesterId);
            var spocUser = ticket.AssignedToUserId.HasValue
                ? await _userRepo.GetByIdAsync(ticket.AssignedToUserId.Value)
                : null;

            var notifUserId = ticket.AssignedToUserId ?? userId;

            // Notifications (always send regardless of requester)
            if (request.StatusId == 2)
            {
                await _notifService.CreateAsync(notifUserId, ticketId, "status_waiting",
                    $"Ticket {ticket.TicketNumber} is waiting for requester",
                    $"Status changed from {fromStatusName} to {toStatusName}");
                if (spocUser != null)
                    await _emailService.SendReplyAsync(spocUser.Email,
                        $"Ticket Waiting | #{ticket.TicketNumber}",
                        $"Ticket {ticket.TicketNumber} is now waiting for requester response.\n\nSubject: {ticket.Subject}");
            }
            else if (request.StatusId == 3)
            {
                await _notifService.CreateAsync(notifUserId, ticketId, "status_resolved",
                    $"Ticket {ticket.TicketNumber} resolved",
                    $"Status changed from {fromStatusName} to {toStatusName}");
                if (spocUser != null)
                    await _emailService.SendReplyAsync(spocUser.Email,
                        $"Ticket Resolved | #{ticket.TicketNumber}",
                        $"Ticket {ticket.TicketNumber} has been marked as resolved.\n\nSubject: {ticket.Subject}");
            }
            else if (request.StatusId == 4)
            {
                await _notifService.CreateAsync(notifUserId, ticketId, "status_closed",
                    $"Ticket {ticket.TicketNumber} closed",
                    $"Status changed from {fromStatusName} to {toStatusName}");
                if (spocUser != null)
                    await _emailService.SendReplyAsync(spocUser.Email,
                        $"Ticket Closed | #{ticket.TicketNumber}",
                        $"Ticket {ticket.TicketNumber} has been closed.\n\nSubject: {ticket.Subject}");
            }
            else if (request.StatusId == 1)
            {
                await _notifService.CreateAsync(notifUserId, ticketId, "status_reopened",
                    $"Ticket {ticket.TicketNumber} reopened",
                    $"Status changed from {fromStatusName} to {toStatusName}");
                if (spocUser != null)
                    await _emailService.SendReplyAsync(spocUser.Email,
                        $"Ticket Reopened | #{ticket.TicketNumber}",
                        $"Ticket {ticket.TicketNumber} has been reopened.\n\nSubject: {ticket.Subject}");
            }
            else
            {
                await _notifService.CreateAsync(notifUserId, ticketId, "status_changed",
                    $"Ticket {ticket.TicketNumber} status changed",
                    $"Status changed from {fromStatusName} to {toStatusName}");
            }

            // Email notifications to requester (only if requester exists)
            if (requester != null)
            {
                if (request.StatusId == 2)
                    await _emailService.SendWaitingEmailAsync(ticket, requester, spocUser);
                else if (request.StatusId == 3)
                    await _emailService.SendResolvedEmailAsync(ticket, requester);
                else if (request.StatusId == 4)
                    await _emailService.SendClosedEmailAsync(ticket, requester);
                else if (request.StatusId == 1)
                    await _emailService.SendReopenedEmailAsync(ticket, requester, spocUser);
                else
                    await _emailService.SendStatusChangeEmailAsync(ticket, fromStatusName, toStatusName);
            }

            await _auditService.LogAsync(
                $"Status changed: {fromStatusName} → {toStatusName}",
                "Ticket", ticketId,
                userId: userId,
                oldValues: fromStatusName,
                newValues: toStatusName);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var statusUpdated = await _ticketRepo.GetTicketListResponseAsync(ticketId);
        if (statusUpdated != null)
            await _hubContext.Clients.All.SendAsync("TicketUpdated", statusUpdated);
    }

    public async Task ReopenAsync(int ticketId, int userId)
    {
        var ticket = await _ticketRepo.GetByIdWithIncludesAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        if (ticket.StatusId != 4)
            throw new InvalidOperationException("Only closed tickets can be reopened");

        var fromStatusId = ticket.StatusId;
        var now = DateTime.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync();

        try
        {
            ticket.StatusId = 1; // In Progress
            ticket.ResolvedAt = null;
            ticket.ClosedAt = null;
            ticket.UpdatedAt = now;
            await _ticketRepo.UpdateAsync(ticket);

            var history = new TicketStatusHistory
            {
                TicketId = ticketId,
                FromStatusId = fromStatusId,
                ToStatusId = 1,
                ChangedByUserId = userId,
                Remarks = "Ticket reopened",
                CreatedAt = now
            };
            await _ticketRepo.AddStatusHistoryAsync(history);

            // Create new SLA with increased priority
            await _slaService.ReopenSlaAsync(ticketId);

            if (ticket.AssignedToUserId.HasValue)
            {
                var spocUser = await _userRepo.GetByIdAsync(ticket.AssignedToUserId.Value);
                if (spocUser != null)
                {
                    await _emailService.SendReopenedEmailAsync(ticket, ticket.Requester, spocUser);
                    await _notifService.CreateAsync(
                        ticket.AssignedToUserId.Value,
                        ticket.Id,
                        "ticket_reopened",
                        $"Ticket {ticket.TicketNumber} reopened",
                        $"Ticket {ticket.TicketNumber} has been reopened"
                    );
                }
            }

            await _auditService.LogAsync("Ticket Reopened", "Ticket", ticket.Id, userId: userId);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var reopenUpdated = await _ticketRepo.GetTicketListResponseAsync(ticketId);
        if (reopenUpdated != null)
            await _hubContext.Clients.All.SendAsync("TicketUpdated", reopenUpdated);
    }

    public async Task<TicketStatsResponse> GetStatsAsync(TicketStatsQueryParams query)
        => await _ticketRepo.GetStatsAsync(query);

    public async Task<List<TicketSlaSummary>> GetSlaSummaryAsync(int? userId = null)
        => await _ticketRepo.GetSlaSummaryAsync(userId);

    public async Task BulkAssignAsync(BulkAssignRequest request)
        => await _ticketRepo.BulkAssignAsync(request.TicketIds, request.AssignedToUserId);

    public async Task<BulkResponse> BulkUpdateStatusAsync(BulkStatusRequest request)
    {
        var now = DateTime.UtcNow;
        var tickets = await _context.Tickets
            .Where(t => request.TicketIds.Contains(t.Id))
            .ToListAsync();

        foreach (var ticket in tickets)
        {
            var fromStatusId = ticket.StatusId;
            ticket.StatusId = request.StatusId;
            ticket.UpdatedAt = now;
            if (request.StatusId == 3) ticket.ResolvedAt = now;
            if (request.StatusId == 4) ticket.ClosedAt = now;
            if (request.StatusId != 3 && request.StatusId != 4) ticket.ResolvedAt = null;

            _context.TicketStatusHistories.Add(new TicketStatusHistory
            {
                TicketId = ticket.Id,
                FromStatusId = fromStatusId,
                ToStatusId = request.StatusId,
                ChangedByUserId = null,
                Remarks = "Bulk status update",
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync();

        foreach (var id in request.TicketIds)
        {
            var bulkUpdated = await _ticketRepo.GetTicketListResponseAsync(id);
            if (bulkUpdated != null)
                await _hubContext.Clients.All.SendAsync("TicketUpdated", bulkUpdated);
        }

        return new BulkResponse { UpdatedCount = request.TicketIds.Count };
    }
}
