using System.Security.Claims;
using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Features.Auth.Commands;
using AIStudyHub.Business.Interfaces.Services;
using AspNet.Security.OAuth.GitHub;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        GoogleDefaults.AuthenticationScheme,
        GitHubAuthenticationDefaults.AuthenticationScheme
    };

    private readonly IMediator _mediator;
    private readonly IAuthService _authService;

    public AuthController(IMediator mediator, IAuthService authService)
    {
        _mediator = mediator;
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResultDto>> Register(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RegisterUserCommand(request), cancellationToken);
        return Ok(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting("AuthLimit")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LoginUserCommand(request), cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken(RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request), cancellationToken);
        return Ok(result);
    }

    [HttpPost("verify-registration-otp")]
    public async Task<IActionResult> VerifyRegistrationOtp(VerifyRegistrationOtpRequestDto request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new VerifyRegistrationOtpCommand(request), cancellationToken);
        return Ok(new { message = "Email verified successfully." });
    }

    [HttpPost("resend-registration-otp")]
    public async Task<IActionResult> ResendRegistrationOtp(ResendOtpRequestDto request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ResendRegistrationOtpCommand(request), cancellationToken);
        return Ok(new { message = "OTP sent successfully." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ForgotPasswordCommand(request), cancellationToken);
        return Ok(new { message = "If the email exists, an OTP has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ResetPasswordCommand(request), cancellationToken);
        return Ok(new { message = "Password reset successfully." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ChangePasswordCommand(request), cancellationToken);
        return Ok(new { message = "Password changed successfully." });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequestDto request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new LogoutCommand(request), cancellationToken);
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("external-login/{provider}")]
    public async Task<IActionResult> ExternalLogin(string provider, [FromServices] Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider schemeProvider)
    {
        var actualProvider = SupportedProviders.FirstOrDefault(p => p.Equals(provider, StringComparison.OrdinalIgnoreCase));
        if (actualProvider == null)
        {
            return BadRequest("Unsupported external provider.");
        }

        var scheme = await schemeProvider.GetSchemeAsync(actualProvider);
        if (scheme == null)
        {
            return BadRequest($"{actualProvider} authentication is not configured on this server.");
        }

        var redirectUrl = Url.Action(nameof(ExternalCallback), new { provider = actualProvider });
        if (string.IsNullOrWhiteSpace(redirectUrl))
        {
            throw new InvalidOperationException("Unable to generate external login callback URL.");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return Challenge(properties, actualProvider);
    }

    [HttpGet("external-callback/{provider}")]
    public async Task<ActionResult<AuthResponseDto>> ExternalCallback(string provider, CancellationToken cancellationToken)
    {
        var actualProvider = SupportedProviders.FirstOrDefault(p => p.Equals(provider, StringComparison.OrdinalIgnoreCase));
        if (actualProvider == null)
        {
            return BadRequest("Unsupported external provider.");
        }

        var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            return Unauthorized();
        }

        var principal = authenticateResult.Principal;
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");
        var fullName = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue("name")
            ?? string.Empty;

        var result = await _authService.LoginExternalAsync(new ExternalLoginRequestDto(actualProvider, email ?? string.Empty, fullName), cancellationToken);
        return Ok(result);
    }
}
