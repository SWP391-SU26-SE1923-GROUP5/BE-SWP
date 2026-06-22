using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Auth.Commands;

public sealed record ForgotPasswordCommand(ForgotPasswordRequestDto Request) : IRequest;

internal sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IAuthService _authService;

    public ForgotPasswordCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        return _authService.ForgotPasswordAsync(request.Request, cancellationToken);
    }
}
