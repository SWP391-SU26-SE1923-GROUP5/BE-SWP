using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Auth.Commands;

public sealed record RegisterUserCommand(RegisterRequestDto Request) : IRequest<RegisterResultDto>;

internal sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterResultDto>
{
    private readonly IAuthService _authService;

    public RegisterUserCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<RegisterResultDto> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        return _authService.RegisterAsync(request.Request, cancellationToken);
    }
}
