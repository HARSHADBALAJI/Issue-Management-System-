using TicketSystem.Api.Models.DTOs.Auth;

namespace TicketSystem.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task LogoutAsync(int userId);
    Task GeneratePasswordResetTokenAsync(int userId);
    Task ResetPasswordAsync(ResetPasswordRequest request);
}
