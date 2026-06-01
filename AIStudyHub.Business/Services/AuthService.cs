using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;

namespace AIStudyHub.Business.Services;

public sealed class AuthService : IAuthService
{
    public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
