using TicketSystem.Api.Data.Repositories;
using TicketSystem.Api.Models.DTOs.Applications;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _appRepo;

    public ApplicationService(IApplicationRepository appRepo) => _appRepo = appRepo;

    public async Task<PagedResponse<ApplicationResponse>> GetPagedAsync(ApplicationQueryParams query)
        => await _appRepo.GetPagedAsync(query);

    public async Task<ApplicationResponse?> GetByIdAsync(int id)
        => await _appRepo.GetApplicationResponseAsync(id);

    public async Task<ApplicationResponse> CreateAsync(CreateApplicationRequest request)
    {
        var entity = new Application
        {
            Name = request.Name,
            IsActive = request.IsActive
        };
        await _appRepo.AddAsync(entity);
        return (await _appRepo.GetApplicationResponseAsync(entity.Id))!;
    }

    public async Task<ApplicationResponse> UpdateAsync(int id, UpdateApplicationRequest request)
    {
        var entity = await _appRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Application {id} not found");
        entity.Name = request.Name;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _appRepo.UpdateAsync(entity);
        return (await _appRepo.GetApplicationResponseAsync(id))!;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _appRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Application {id} not found");

        var ticketCount = await _appRepo.GetTicketCountAsync(id);
        if (ticketCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete '{entity.Name}': {ticketCount} ticket(s) are associated with this application.");

        await _appRepo.HardDeleteAsync(id);
    }

    public async Task<List<LookupItem>> GetLookupAsync()
        => await _appRepo.GetLookupAsync();

    public async Task UpdateUsersAsync(int applicationId, List<int> userIds, int? primarySpocUserId = null)
        => await _appRepo.UpdateUsersAsync(applicationId, userIds, primarySpocUserId);
}
