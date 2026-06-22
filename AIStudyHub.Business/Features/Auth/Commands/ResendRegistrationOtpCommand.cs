using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Auth.Commands;

public sealed record ResendRegistrationOtpCommand(ResendOtpRequestDto Request) : IRequest;

internal sealed class ResendRegistrationOtpCommandHandler : IRequestHandler<ResendRegistrationOtpCommand>
{
    private readonly IAuthService _authService;

    public ResendRegistrationOtpCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task Handle(ResendRegistrationOtpCommand request, CancellationToken cancellationToken)
    {
        return _authService.ResendRegistrationOtpAsync(request.Request, cancellationToken);
    }
}
