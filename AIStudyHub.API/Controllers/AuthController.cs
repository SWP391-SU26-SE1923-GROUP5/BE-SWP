using System.Security.Claims;
using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Features.Auth;
using AIStudyHub.Business.Interfaces.Services;
using AspNet.Security.OAuth.GitHub;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RegisterUserCommand(request), cancellationToken);
        return Ok(result);
    }

    [HttpPost("login")]
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

    [HttpGet("external-login/{provider}")]
    public IActionResult ExternalLogin(string provider)
    {
        if (!SupportedProviders.Contains(provider))
        {
            return BadRequest("Unsupported external provider.");
        }

        var redirectUrl = Url.Action(nameof(ExternalCallback), new { provider });
        if (string.IsNullOrWhiteSpace(redirectUrl))
        {
            throw new InvalidOperationException("Unable to generate external login callback URL.");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return Challenge(properties, provider);
    }

    [HttpGet("external-callback/{provider}")]
    public async Task<ActionResult<AuthResponseDto>> ExternalCallback(string provider, CancellationToken cancellationToken)
    {
        if (!SupportedProviders.Contains(provider))
        {
            return BadRequest("Unsupported external provider.");
        }

        var authenticateResult = await HttpContext.AuthenticateAsync(provider);
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

        var result = await _authService.LoginExternalAsync(new ExternalLoginRequestDto(provider, email ?? string.Empty, fullName), cancellationToken);
        return Ok(result);
    }
}
