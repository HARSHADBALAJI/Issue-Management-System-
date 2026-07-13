using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TicketSystem.Api.Data.Repositories;
using TicketSystem.Api.Models.DTOs.Auth;

namespace TicketSystem.Api.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;

    public AuthService(IUserRepository userRepo, IConfiguration config)
    {
        _userRepo = userRepo;
        _config = config;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepo.Query()
            .Include(u => u.Role)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive)
            ?? throw new UnauthorizedAccessException("Invalid email or password");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password");

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user);

        var (token, expiresAt) = GenerateJwtToken(user);
        var refreshToken = Guid.NewGuid().ToString("N");

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = expiresAt.AddDays(7);
        await _userRepo.UpdateAsync(user);

        return new LoginResponse
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new LoginResponse.UserInfo
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.Name,
                DepartmentId = user.DepartmentId,
                DepartmentName = user.Department.Name
            }
        };
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var user = await _userRepo.Query()
            .Include(u => u.Role)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken && u.IsActive)
            ?? throw new UnauthorizedAccessException("Invalid refresh token");

        if (user.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired");

        var (token, expiresAt) = GenerateJwtToken(user);
        var newRefreshToken = Guid.NewGuid().ToString("N");

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = expiresAt.AddDays(7);
        await _userRepo.UpdateAsync(user);

        return new LoginResponse
        {
            AccessToken = token,
            RefreshToken = newRefreshToken,
            ExpiresAt = expiresAt,
            User = new LoginResponse.UserInfo
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.Name,
                DepartmentId = user.DepartmentId,
                DepartmentName = user.Department.Name
            }
        };
    }

    public async Task LogoutAsync(int userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            await _userRepo.UpdateAsync(user);
        }
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
        var user = await _userRepo.Query()
            .FirstOrDefaultAsync(u => u.RefreshToken == request.Token)
            ?? throw new InvalidOperationException("Invalid token");

        if (user.RefreshTokenExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Token expired");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _userRepo.UpdateAsync(user);
    }

    private (string token, DateTime expiresAt) GenerateJwtToken(Models.Entities.User user)
    {
        var jwtKey = _config["Jwt:Key"] ?? "SuperSecretKeyForTicketSystem2024!@#$%";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var expiresAt = DateTime.UtcNow.AddMinutes(
            double.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "60"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role?.Name ?? "SPOC"),
            new Claim("DepartmentId", user.DepartmentId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "TicketSystem",
            audience: _config["Jwt:Audience"] ?? "TicketSystem",
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
