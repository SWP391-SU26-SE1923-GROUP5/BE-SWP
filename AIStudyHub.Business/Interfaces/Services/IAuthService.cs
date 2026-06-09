using AIStudyHub.Business.DTOs.Authentication;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IAuthService
{
    Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginExternalAsync(ExternalLoginRequestDto request, CancellationToken cancellationToken = default);
    Task ConfirmEmailAsync(ConfirmEmailRequestDto request, CancellationToken cancellationToken = default);
    Task ResendEmailVerificationAsync(ResendEmailVerificationRequestDto request, CancellationToken cancellationToken = default);
}
