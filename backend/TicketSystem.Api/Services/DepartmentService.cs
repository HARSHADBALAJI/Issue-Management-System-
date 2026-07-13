using TicketSystem.Api.Data.Repositories;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Departments;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _repo;

    public DepartmentService(IDepartmentRepository repo) => _repo = repo;

    public async Task<PagedResponse<DepartmentResponse>> GetPagedAsync(DepartmentQueryParams query)
        => await _repo.GetPagedAsync(query);

    public async Task<DepartmentResponse?> GetByIdAsync(int id)
        => await _repo.GetDepartmentResponseAsync(id);

    public async Task<DepartmentResponse> CreateAsync(CreateDepartmentRequest request)
    {
        var existing = await _repo.FindAsync(d => d.Name == request.Name);
        if (existing.Count != 0)
            throw new InvalidOperationException($"A department with the name '{request.Name}' already exists.");
        var entity = new Department { Name = request.Name, HeadName = request.HeadName, IsActive = true };
        await _repo.AddAsync(entity);
        return (await _repo.GetDepartmentResponseAsync(entity.Id))!;
    }

    public async Task<DepartmentResponse> UpdateAsync(int id, UpdateDepartmentRequest request)
    {
        var entity = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Department {id} not found");
        entity.Name = request.Name;
        if (request.HeadName != null) entity.HeadName = request.HeadName;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(entity);
        return (await _repo.GetDepartmentResponseAsync(id))!;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Department {id} not found");
        await _repo.DeleteAsync(entity);
    }

    public async Task<List<LookupItem>> GetLookupAsync()
        => await _repo.FindAsync(d => d.IsActive)
            .ContinueWith(t => t.Result.Select(d => new LookupItem { Id = d.Id, Name = d.Name }).ToList());
}
