using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;

namespace TicketSystem.Api.Services;

public class TicketAssignmentService : ITicketAssignmentService
{
    private readonly TicketSystemDbContext _context;
    private readonly ILogger<TicketAssignmentService> _logger;

    public TicketAssignmentService(TicketSystemDbContext context, ILogger<TicketAssignmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int?> GetPrimarySpocUserIdAsync(int applicationId)
    {
        var routing = await _context.ApplicationRoutingRules
            .AsNoTracking()
            .Where(r => r.ApplicationId == applicationId && r.IsActive)
            .OrderBy(r => r.Id)
            .FirstOrDefaultAsync();

        if (routing != null)
        {
            _logger.LogInformation("Assigned SPOC UserId={UserId} for ApplicationId={ApplicationId}",
                routing.PrimarySpocUserId, applicationId);
            return routing.PrimarySpocUserId;
        }

        var fallback = await _context.ApplicationAssignments
            .AsNoTracking()
            .Where(aa => aa.ApplicationId == applicationId && aa.User.IsActive)
            .OrderBy(aa => aa.IsPrimarySPOC ? 0 : 1)
                .ThenBy(aa => aa.AssignedAt)
            .Select(aa => aa.UserId)
            .FirstOrDefaultAsync();

        if (fallback != 0)
        {
            _logger.LogInformation("Fallback SPOC UserId={UserId} for ApplicationId={ApplicationId} (from ApplicationAssignment)",
                fallback, applicationId);
            return fallback;
        }

        _logger.LogWarning("No active routing rule or assigned users found for ApplicationId={ApplicationId}", applicationId);
        return null;
    }
}
