namespace TicketSystem.Api.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, int? entityId,
        int? userId = null, int? requesterId = null,
        string? oldValues = null, string? newValues = null,
        string? ipAddress = null);
}
