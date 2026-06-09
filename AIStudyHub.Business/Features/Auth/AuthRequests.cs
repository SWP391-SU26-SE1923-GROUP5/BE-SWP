using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Auth;

public sealed record RegisterUserCommand(RegisterRequestDto Request) : IRequest<RegisterResultDto>;

public sealed record LoginUserCommand(LoginRequestDto Request) : IRequest<AuthResponseDto>;

public sealed record RefreshTokenCommand(RefreshTokenRequestDto Request) : IRequest<AuthResponseDto>;

public sealed record ConfirmEmailCommand(ConfirmEmailRequestDto Request) : IRequest;

public sealed record ResendEmailVerificationCommand(ResendEmailVerificationRequestDto Request) : IRequest;

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

internal sealed class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, AuthResponseDto>
{
    private readonly IAuthService _authService;

    public LoginUserCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<AuthResponseDto> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        return _authService.LoginAsync(request.Request, cancellationToken);
    }
}

internal sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly IAuthService _authService;

    public RefreshTokenCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        return _authService.RefreshTokenAsync(request.Request, cancellationToken);
    }
}

internal sealed class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand>
{
    private readonly IAuthService _authService;

    public ConfirmEmailCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        return _authService.ConfirmEmailAsync(request.Request, cancellationToken);
    }
}

internal sealed class ResendEmailVerificationCommandHandler : IRequestHandler<ResendEmailVerificationCommand>
{
    private readonly IAuthService _authService;

    public ResendEmailVerificationCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task Handle(ResendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        return _authService.ResendEmailVerificationAsync(request.Request, cancellationToken);
    }
}
