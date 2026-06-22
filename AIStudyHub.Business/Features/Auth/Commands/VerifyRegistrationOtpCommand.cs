using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Auth.Commands;

public sealed record VerifyRegistrationOtpCommand(VerifyRegistrationOtpRequestDto Request) : IRequest;

internal sealed class VerifyRegistrationOtpCommandHandler : IRequestHandler<VerifyRegistrationOtpCommand>
{
    private readonly IAuthService _authService;

    public VerifyRegistrationOtpCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task Handle(VerifyRegistrationOtpCommand request, CancellationToken cancellationToken)
    {
        return _authService.VerifyRegistrationOtpAsync(request.Request, cancellationToken);
    }
}
