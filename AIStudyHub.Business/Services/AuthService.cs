using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace AIStudyHub.Business.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly IMapper _mapper;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        UserManager<User> userManager,
        IMapper mapper,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        JwtOptions jwtOptions)
    {
        _userManager = userManager;
        _mapper = mapper;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _jwtOptions = jwtOptions;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = NormalizeEmail(request.Email);
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            DateOfBirth = request.DateOfBirth,
            CurrentStorageCapacity = 0,
            CurrentAiToken = 0,
            Status = "Active",
            Role = UserRole.Student,
            IsActive = true,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        EnsureIdentitySucceeded(createResult);

        var roleResult = await _userManager.AddToRoleAsync(user, UserRole.Student.ToString());
        EnsureIdentitySucceeded(roleResult);

        return await CreateAuthResponseAsync(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("User account is inactive.");
        }

        return await CreateAuthResponseAsync(user);
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var accessToken = GenerateAccessToken(user, roles, expiresAt);
        var response = _mapper.Map<UserResponseDto>(user);

        return new AuthResponseDto(response, accessToken, expiresAt);
    }

    private string GenerateAccessToken(User user, IEnumerable<string> roles, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SecretKey))
        {
            throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void EnsureIdentitySucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException(errors);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
