using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Auth.Commands;

public sealed record ConfirmResetPasswordCommand(ConfirmResetPasswordRequestDto Request) : IRequest;

internal sealed class ConfirmResetPasswordCommandHandler : IRequestHandler<ConfirmResetPasswordCommand>
{
    private readonly IAuthService _authService;

    public ConfirmResetPasswordCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task Handle(ConfirmResetPasswordCommand request, CancellationToken cancellationToken)
    {
        return _authService.ConfirmResetPasswordAsync(request.Request, cancellationToken);
    }
}
