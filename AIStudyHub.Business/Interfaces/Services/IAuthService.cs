using AIStudyHub.Business.DTOs.Authentication;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginExternalAsync(ExternalLoginRequestDto request, CancellationToken cancellationToken = default);
}
