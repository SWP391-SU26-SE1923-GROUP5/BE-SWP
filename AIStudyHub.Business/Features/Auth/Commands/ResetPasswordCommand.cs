using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Auth.Commands;

public sealed record ResetPasswordCommand(ResetPasswordRequestDto Request) : IRequest;

internal sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IAuthService _authService;

    public ResetPasswordCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        return _authService.ResetPasswordAsync(request.Request, cancellationToken);
    }
}
