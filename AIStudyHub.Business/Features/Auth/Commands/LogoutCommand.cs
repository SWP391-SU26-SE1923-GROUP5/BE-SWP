using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Auth.Commands;

public sealed record LogoutCommand(LogoutRequestDto Request) : IRequest;

internal sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IAuthService _authService;

    public LogoutCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        return _authService.LogoutAsync(request.Request, cancellationToken);
    }
}
