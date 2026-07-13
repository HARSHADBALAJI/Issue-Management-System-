using TicketSystem.Api.Data;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class AuditService : IAuditService
{
    private readonly TicketSystemDbContext _context;

    public AuditService(TicketSystemDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string action, string entityType, int? entityId,
        int? userId = null, int? requesterId = null,
        string? oldValues = null, string? newValues = null,
        string? ipAddress = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            RequesterId = requesterId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
