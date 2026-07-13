using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Data;
using TicketSystem.Api.Data.Repositories;
using TicketSystem.Api.Models.DTOs.Auth;
using TicketSystem.Api.Models.DTOs.Common;
using TicketSystem.Api.Models.DTOs.Users;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IDepartmentRepository _deptRepo;
    private readonly IRepository<Role> _roleRepo;

    public UserService(IUserRepository userRepo, IDepartmentRepository deptRepo, IRepository<Role> roleRepo)
    {
        _userRepo = userRepo;
        _deptRepo = deptRepo;
        _roleRepo = roleRepo;
    }

    public async Task<PagedResponse<UserResponse>> GetPagedAsync(UserQueryParams query)
        => await _userRepo.GetPagedAsync(query);

    public async Task<UserResponse?> GetByIdAsync(int id)
        => await _userRepo.GetUserResponseAsync(id);

    public async Task<UserResponse> CreateAsync(CreateUserRequest request)
    {
        if (await _userRepo.GetByEmailAsync(request.Email) != null)
            throw new InvalidOperationException("Email already exists");

        var role = await _roleRepo.FindAsync(r => r.Name.ToLower() == request.Role.ToLower());
        var roleEntity = role.FirstOrDefault()
            ?? throw new KeyNotFoundException($"Role '{request.Role}' not found");

        int departmentId = await ResolveDepartmentId(request.DepartmentName);

        var entity = new User
        {
            EmployeeId = Guid.NewGuid().ToString("N")[..8],
            FullName = request.Name,
            Email = request.Email,
            DepartmentId = departmentId,
            RoleId = roleEntity.Id,
            IsActive = request.Status.ToLower() == "active",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
        };

        await _userRepo.AddAsync(entity);
        return (await _userRepo.GetUserResponseAsync(entity.Id))!;
    }

    public async Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request)
    {
        var entity = await _userRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found");

        if (request.Email != entity.Email && await _userRepo.GetByEmailAsync(request.Email) != null)
            throw new InvalidOperationException("Email already in use");

        var role = await _roleRepo.FindAsync(r => r.Name.ToLower() == request.Role.ToLower());
        var roleEntity = role.FirstOrDefault()
            ?? throw new KeyNotFoundException($"Role '{request.Role}' not found");

        int oldDepartmentId = entity.DepartmentId;
        int newDepartmentId = await ResolveDepartmentId(request.DepartmentName);

        entity.FullName = request.Name;
        entity.Email = request.Email;
        entity.DepartmentId = newDepartmentId;
        entity.RoleId = roleEntity.Id;
        entity.IsActive = request.Status.ToLower() == "active";
        entity.UpdatedAt = DateTime.UtcNow;

        await _userRepo.UpdateAsync(entity);

        if (oldDepartmentId != newDepartmentId)
            await _userRepo.UpdateRoutingRulesForUserAsync(id, oldDepartmentId, newDepartmentId);

        return (await _userRepo.GetUserResponseAsync(id))!;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _userRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found");
        await _userRepo.DeleteAsync(entity);
    }

    public async Task<List<LookupItem>> GetLookupAsync(int? roleId = null)
        => await _userRepo.GetLookupAsync(roleId);

    public async Task UpdateApplicationsAsync(int userId, List<int> applicationIds)
    {
        if (await _userRepo.GetByIdAsync(userId) == null)
            throw new KeyNotFoundException($"User {userId} not found");
        await _userRepo.UpdateApplicationsAsync(userId, applicationIds);
    }

    public async Task GeneratePasswordResetTokenAsync(int userId)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found");

        user.RefreshToken = Guid.NewGuid().ToString("N");
        user.RefreshTokenExpiry = DateTime.UtcNow.AddHours(24);
        await _userRepo.UpdateAsync(user);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _userRepo.FindAsync(u => u.RefreshToken == request.Token);
        var entity = user.FirstOrDefault()
            ?? throw new InvalidOperationException("Invalid token");

        if (entity.RefreshTokenExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Token expired");

        entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        entity.RefreshToken = null;
        entity.RefreshTokenExpiry = null;
        await _userRepo.UpdateAsync(entity);
    }

    private async Task<int> ResolveDepartmentId(string? departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
        {
            var firstDept = await _deptRepo.FindAsync(d => true);
            return firstDept.FirstOrDefault()?.Id
                ?? throw new KeyNotFoundException("No departments exist");
        }

        var depts = await _deptRepo.FindAsync(d => d.Name == departmentName);
        return depts.FirstOrDefault()?.Id
            ?? throw new KeyNotFoundException($"Department '{departmentName}' not found");
    }
}
