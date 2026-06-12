using System.Security.Claims;
using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AIStudyHub.Business.Features.Auth;

public sealed record RegisterUserCommand(RegisterRequestDto Request) : IRequest<RegisterResultDto>;

public sealed record LoginUserCommand(LoginRequestDto Request) : IRequest<AuthResponseDto>;

public sealed record RefreshTokenCommand(RefreshTokenRequestDto Request) : IRequest<AuthResponseDto>;

public sealed record VerifyRegistrationOtpCommand(VerifyRegistrationOtpRequestDto Request) : IRequest;

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

public sealed record ChangePasswordCommand(ChangePasswordRequestDto Request) : IRequest;

internal sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IAuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChangePasswordCommandHandler(IAuthService authService, IHttpContextAccessor httpContextAccessor)
    {
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        await _authService.ChangePasswordAsync(user!, request.Request, cancellationToken);
    }
}

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
