using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Auth.Commands;

public sealed record VerifyPasswordResetOtpCommand(VerifyPasswordResetOtpRequestDto Request) : IRequest;

internal sealed class VerifyPasswordResetOtpCommandHandler : IRequestHandler<VerifyPasswordResetOtpCommand>
{
    private readonly IAuthService _authService;

    public VerifyPasswordResetOtpCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task Handle(VerifyPasswordResetOtpCommand request, CancellationToken cancellationToken)
    {
        return _authService.VerifyPasswordResetOtpAsync(request.Request, cancellationToken);
    }
}
