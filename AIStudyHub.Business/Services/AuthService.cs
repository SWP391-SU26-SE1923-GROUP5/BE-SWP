using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AIStudyHub.Business.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<RefreshTokenRequestDto> _refreshTokenValidator;
    private readonly IValidator<ConfirmEmailRequestDto> _confirmEmailValidator;
    private readonly IValidator<ResendEmailVerificationRequestDto> _resendEmailVerificationValidator;
    private readonly JwtOptions _jwtOptions;
    private readonly IEmailService _emailService;
    private readonly EmailVerificationOptions _emailVerificationOptions;

    public AuthService(
        UserManager<User> userManager,
        ApplicationDbContext dbContext,
        IMapper mapper,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<RefreshTokenRequestDto> refreshTokenValidator,
        IValidator<ConfirmEmailRequestDto> confirmEmailValidator,
        IValidator<ResendEmailVerificationRequestDto> resendEmailVerificationValidator,
        JwtOptions jwtOptions,
        IEmailService emailService,
        EmailVerificationOptions emailVerificationOptions)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _mapper = mapper;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshTokenValidator = refreshTokenValidator;
        _confirmEmailValidator = confirmEmailValidator;
        _resendEmailVerificationValidator = resendEmailVerificationValidator;
        _jwtOptions = jwtOptions;
        _emailService = emailService;
        _emailVerificationOptions = emailVerificationOptions;
    }

    public async Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = NormalizeEmail(request.Email);
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = BuildStudentUser(normalizedEmail, request.FullName, request.DateOfBirth, emailConfirmed: false);
        var createResult = await _userManager.CreateAsync(user, request.Password);
        EnsureIdentitySucceeded(createResult);

        await EnsureStudentRoleAsync(user);
        await SendEmailVerificationAsync(user, cancellationToken);

        return new RegisterResultDto("Registration successful. Please verify your email before logging in.", normalizedEmail);
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

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedAccessException("Email address has not been verified.");
        }

        EnsureUserIsActive(user);
        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        await _refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || !storedToken.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var user = storedToken.User;
        EnsureUserIsActive(user);

        var roles = await _userManager.GetRolesAsync(user);
        var newRefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);
        var newRefreshToken = GenerateRefreshToken(user, roles, newRefreshTokenExpiresAt);
        var newRefreshTokenHash = HashRefreshToken(newRefreshToken);

        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReplacedByTokenHash = newRefreshTokenHash;

        await _dbContext.RefreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = newRefreshTokenExpiresAt
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await CreateAuthResponseAsync(user, newRefreshToken, newRefreshTokenExpiresAt);
    }

    public async Task<AuthResponseDto> LoginExternalAsync(ExternalLoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException($"{request.Provider} account did not provide an email address.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null)
        {
            var fullName = string.IsNullOrWhiteSpace(request.FullName)
                ? normalizedEmail.Split('@')[0]
                : request.FullName.Trim();

            user = BuildStudentUser(normalizedEmail, fullName, null, emailConfirmed: true);
            var tempPassword = $"Ext#{Guid.NewGuid():N}aA1!";
            var createResult = await _userManager.CreateAsync(user, tempPassword);
            EnsureIdentitySucceeded(createResult);
            await EnsureStudentRoleAsync(user);
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            var updateResult = await _userManager.UpdateAsync(user);
            EnsureIdentitySucceeded(updateResult);
        }

        EnsureUserIsActive(user);
        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequestDto request, CancellationToken cancellationToken = default)
    {
        await _confirmEmailValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new InvalidOperationException("Invalid email verification request.");
        }

        var user = await _userManager.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("Invalid email verification request.");
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        var decodedToken = DecodeToken(request.Token);
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        EnsureIdentitySucceeded(result);
    }

    public async Task ResendEmailVerificationAsync(ResendEmailVerificationRequestDto request, CancellationToken cancellationToken = default)
    {
        await _resendEmailVerificationValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null)
        {
            return;
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        await SendEmailVerificationAsync(user, cancellationToken);
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);
        var refreshToken = GenerateRefreshToken(user, roles, refreshTokenExpiresAt);

        await _dbContext.RefreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            ExpiresAt = refreshTokenExpiresAt
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await CreateAuthResponseAsync(user, refreshToken, refreshTokenExpiresAt);
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(User user, string refreshToken, DateTime refreshTokenExpiresAt)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var accessToken = GenerateAccessToken(user, roles, accessTokenExpiresAt);
        var response = _mapper.Map<UserResponseDto>(user);

        return new AuthResponseDto(response, accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt);
    }

    private static User BuildStudentUser(string normalizedEmail, string fullName, DateOnly? dateOfBirth, bool emailConfirmed)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            DateOfBirth = dateOfBirth,
            CurrentStorageCapacity = 0,
            CurrentAiTokenUsage = 0,
            Status = "active",
            Role = "student",
            IsActive = true,
            EmailConfirmed = emailConfirmed
        };
    }

    private async Task EnsureStudentRoleAsync(User user)
    {
        var roleResult = await _userManager.AddToRoleAsync(user, "Student");
        EnsureIdentitySucceeded(roleResult);
    }

    private async Task SendEmailVerificationAsync(User user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_emailVerificationOptions.VerificationBaseUrl))
        {
            throw new InvalidOperationException("Email verification URL is not configured.");
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = EncodeToken(token);
        var verificationUrl = BuildVerificationUrl(user.Id, encodedToken);
        var htmlBody = $"<p>Hello {System.Net.WebUtility.HtmlEncode(user.FullName)},</p><p>Please verify your email by clicking the link below:</p><p><a href=\"{verificationUrl}\">Verify email</a></p><p>If you did not create this account, you can ignore this email.</p>";

        await _emailService.SendAsync(user.Email!, "Verify your AIStudyHub email", htmlBody, cancellationToken);
    }

    private string BuildVerificationUrl(Guid userId, string token)
    {
        return QueryHelpers.AddQueryString(_emailVerificationOptions.VerificationBaseUrl, new Dictionary<string, string?>
        {
            ["userId"] = userId.ToString(),
            ["token"] = token
        });
    }

    private static string EncodeToken(string token)
    {
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    private static string DecodeToken(string token)
    {
        try
        {
            var decodedBytes = WebEncoders.Base64UrlDecode(token);
            return Encoding.UTF8.GetString(decodedBytes);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Invalid email verification request.");
        }
    }

    private static void EnsureUserIsActive(User user)
    {
        if (!user.IsActive || !string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("User account is inactive.");
        }
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

    private string GenerateRefreshToken(User user, IEnumerable<string> roles, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SecretKey))
        {
            throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("type", "refresh")
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }
}
