using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data.Entities;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AIStudyHub.Data;

namespace AIStudyHub.Business.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<RefreshTokenRequestDto> _refreshTokenValidator;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        UserManager<User> userManager,
        ApplicationDbContext dbContext,
        IMapper mapper,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<RefreshTokenRequestDto> refreshTokenValidator,
        JwtOptions jwtOptions)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _mapper = mapper;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshTokenValidator = refreshTokenValidator;
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
            CurrentAiTokenUsage = 0,
            Status = "active",
            Role = "student",
            IsActive = true,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        EnsureIdentitySucceeded(createResult);

        var roleResult = await _userManager.AddToRoleAsync(user, "Student");
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

        if (!user.IsActive || !string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("User account is inactive.");
        }

        return await CreateAuthResponseAsync(user);
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

        if (!user.IsActive || !string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("User account is inactive.");
        }

        var newRefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);
        var newRefreshToken = GenerateRefreshToken(user, newRefreshTokenExpiresAt);
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

    private async Task<AuthResponseDto> CreateAuthResponseAsync(User user)
    {
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);
        var refreshToken = GenerateRefreshToken(user, refreshTokenExpiresAt);

        await _dbContext.RefreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            ExpiresAt = refreshTokenExpiresAt
        });

        await _dbContext.SaveChangesAsync();

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

    private string GenerateRefreshToken(User user, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SecretKey))
        {
            throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        }

        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var entropyBytes = Encoding.UTF8.GetBytes($"{user.Id}:{expiresAt:O}:{Guid.NewGuid()}");
        var buffer = new byte[randomBytes.Length + entropyBytes.Length];
        Buffer.BlockCopy(randomBytes, 0, buffer, 0, randomBytes.Length);
        Buffer.BlockCopy(entropyBytes, 0, buffer, randomBytes.Length, entropyBytes.Length);
        return Convert.ToBase64String(buffer);
    }

    private static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }
}
