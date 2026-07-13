using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AIStudyHub.Business.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly JwtOptions _jwtOptions;
    private readonly IEmailService _emailService;
    private readonly EmailVerificationOptions _emailVerificationOptions;
    private readonly OtpOptions _otpOptions;
    private readonly AIStudyHub.Business.Interfaces.Services.IGamificationService? _gamificationService;
    private readonly ILogger<AuthService>? _logger;

    public AuthService(
        UserManager<User> userManager,
        ApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        JwtOptions jwtOptions,
        IEmailService emailService,
        EmailVerificationOptions emailVerificationOptions,
        OtpOptions otpOptions,
        AIStudyHub.Business.Interfaces.Services.IGamificationService? gamificationService = null,
        ILogger<AuthService>? logger = null)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _jwtOptions = jwtOptions;
        _emailService = emailService;
        _emailVerificationOptions = emailVerificationOptions;
        _otpOptions = otpOptions;
        _gamificationService = gamificationService;
        _logger = logger;
    }

    public async Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = await BuildStudentUserAsync(normalizedEmail, request.FullName, request.DateOfBirth, cancellationToken);
        var createResult = await _userManager.CreateAsync(user, request.Password);
        EnsureIdentitySucceeded(createResult);

        await EnsureStudentRoleAsync(user);
        await SendEmailVerificationOtpAsync(user, cancellationToken);

        // Wrap UserStats creation so a failure here doesn't fail registration.
        if (_gamificationService is not null)
        {
            try
            {
                await _gamificationService.EnsureUserStatsAsync(user.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to ensure UserStats for newly registered user {UserId}", user.Id);
            }
        }

        return new RegisterResultDto("Registration successful. Please verify your email within 3 minutes.", normalizedEmail);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.EmailConfirmed)
        {
            throw new InvalidOperationException("Please verify your email before logging in.");
        }

        EnsureUserIsActive(user);
        await _dbContext.Entry(user).Reference(u => u.TierMembership).LoadAsync(cancellationToken);
        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .Include(refreshToken => refreshToken.User)
                .ThenInclude(user => user.TierMembership)
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            _logger?.LogWarning("Refresh token validation failed: token not found in database");
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (!storedToken.IsActive)
        {
            _logger?.LogWarning("Refresh token validation failed: token is inactive (expired or revoked)");
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

            user = await BuildStudentUserAsync(normalizedEmail, fullName, null, cancellationToken);
            user.EmailConfirmed = true; // OAuth provider verified email
            var tempPassword = $"Ext#{Guid.NewGuid():N}aA1!";
            var createResult = await _userManager.CreateAsync(user, tempPassword);
            EnsureIdentitySucceeded(createResult);
            await EnsureStudentRoleAsync(user);
        }
        else
        {
            EnsureUserIsActive(user);
        }

        await _dbContext.Entry(user).Reference(u => u.TierMembership).LoadAsync(cancellationToken);
        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task VerifyRegistrationOtpAsync(VerifyRegistrationOtpRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            throw new InvalidOperationException("Invalid email verification request.");
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        var otpRecord = await _dbContext.OtpRecords
            .Where(o => o.Email == normalizedEmail && o.UserId == user.Id && o.Type == OtpType.EmailVerification && o.UsedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpRecord is null)
        {
            throw new OtpInvalidException();
        }

        if (otpRecord.IsLocked)
        {
            throw new OtpLockedException(OtpRecord.LockoutMinutes);
        }

        if (otpRecord.IsExpired)
        {
            throw new OtpExpiredException();
        }

        if (!VerifyOtp(request.Otp, otpRecord.OtpHash))
        {
            otpRecord.FailedAttempts++;
            if (otpRecord.FailedAttempts >= OtpRecord.MaxFailedAttempts)
            {
                otpRecord.LockedUntil = DateTime.UtcNow.AddMinutes(OtpRecord.LockoutMinutes);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new OtpInvalidException("Invalid OTP. Please try again.");
        }

        otpRecord.UsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);
    }

    public async Task ResendRegistrationOtpAsync(ResendOtpRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        if (user.EmailConfirmed)
        {
            throw new InvalidOperationException("Email is already verified.");
        }

        var otp = await SendOtpAsync(normalizedEmail, user.Id, OtpType.EmailVerification, cancellationToken);

        await _emailService.SendAsync(
            normalizedEmail,
            "AI Study Hub - Email Verification Code",
            BuildRegistrationEmailBody(user.FullName, otp),
            cancellationToken);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            return;
        }

        var otp = await SendOtpAsync(normalizedEmail, user.Id, OtpType.PasswordReset, cancellationToken);

        await _emailService.SendAsync(
            normalizedEmail,
            "AI Study Hub - Password Reset OTP",
            BuildPasswordResetEmailBody(user.FullName, otp),
            cancellationToken);
    }

    public async Task VerifyPasswordResetOtpAsync(VerifyPasswordResetOtpRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            throw new OtpInvalidException("Invalid or expired OTP.");
        }

        var otpRecord = await _dbContext.OtpRecords
            .Where(o => o.Email == normalizedEmail && o.UserId == user.Id && o.Type == OtpType.PasswordReset && o.UsedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpRecord is null)
        {
            throw new OtpInvalidException();
        }

        if (otpRecord.IsLocked)
        {
            throw new OtpLockedException(OtpRecord.LockoutMinutes);
        }

        if (otpRecord.IsExpired)
        {
            throw new OtpExpiredException();
        }

        if (!VerifyOtp(request.Otp, otpRecord.OtpHash))
        {
            otpRecord.FailedAttempts++;
            if (otpRecord.FailedAttempts >= OtpRecord.MaxFailedAttempts)
            {
                otpRecord.LockedUntil = DateTime.UtcNow.AddMinutes(OtpRecord.LockoutMinutes);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new OtpInvalidException("Invalid OTP. Please try again.");
        }

        otpRecord.UsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmResetPasswordAsync(ConfirmResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            throw new OtpInvalidException("Invalid or expired OTP.");
        }

        var otpRecord = await _dbContext.OtpRecords
            .Where(o => o.Email == normalizedEmail && o.UserId == user.Id && o.Type == OtpType.PasswordReset && o.UsedAt != null)
            .OrderByDescending(o => o.UsedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpRecord is null)
        {
            throw new OtpInvalidException("No verified OTP found. Please verify your email first.");
        }

        if (otpRecord.IsExpired)
        {
            throw new OtpInvalidException("OTP has expired. Please request a new one.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        EnsureIdentitySucceeded(result);

        await _dbContext.OtpRecords
            .Where(o => o.Email == normalizedEmail && o.UserId == user.Id && o.Type == OtpType.PasswordReset)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(ClaimsPrincipal userPrincipal, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var user = await _userManager.FindByIdAsync(userGuid.ToString());
        if (user is null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!isCurrentPasswordValid)
        {
            throw new UnauthorizedAccessException("Current password is incorrect.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        EnsureIdentitySucceeded(result);
    }

    public async Task LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken is not null)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GenerateOtp()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes);
    }

    private static bool VerifyOtp(string input, string storedHash)
    {
        var inputHash = HashOtp(input);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(inputHash),
            Encoding.UTF8.GetBytes(storedHash));
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

    private async Task<User> BuildStudentUserAsync(string normalizedEmail, string fullName, DateOnly? dateOfBirth, CancellationToken cancellationToken = default)
    {
        var freeTier = await _unitOfWork.TierMemberships
            .Query()
            .FirstOrDefaultAsync(t => t.TierName == "Free", cancellationToken);
        if (freeTier is null)
        {
            throw new InvalidOperationException("Free tier not found in database. Please run seed data.");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            DateOfBirth = dateOfBirth,
            TierId = freeTier.Id,
            CurrentStorageCapacity = 0,
            CurrentAiTokenUsage = 0,
            Status = "active",
            Role = "student",
            IsActive = true,
            EmailConfirmed = false
        };
    }

    private async Task EnsureStudentRoleAsync(User user)
    {
        var roleResult = await _userManager.AddToRoleAsync(user, "Student");
        EnsureIdentitySucceeded(roleResult);
    }

    private async Task SendEmailVerificationOtpAsync(User user, CancellationToken cancellationToken)
    {
        var otp = await SendOtpAsync(user.Email!, user.Id, OtpType.EmailVerification, cancellationToken);

        await _emailService.SendAsync(
            user.Email!,
            "AI Study Hub - Email Verification Code",
            BuildRegistrationEmailBody(user.FullName, otp),
            cancellationToken);
    }

    private static void EnsureUserIsActive(User user)
    {
        if (!user.IsActive || !string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("User account is inactive.");
        }
    }

    private async Task<string> SendOtpAsync(string email, Guid userId, OtpType type, CancellationToken cancellationToken = default)
    {
        var existingOtps = await _dbContext.OtpRecords
            .Where(o => o.Email == email && o.UserId == userId && o.Type == type && o.ExpiresAt > DateTime.UtcNow && o.UsedAt == null)
            .ToListAsync(cancellationToken);

        var recentSendCount = existingOtps.Count(o => o.CreatedAt >= DateTime.UtcNow.AddMinutes(-_otpOptions.SendWindowMinutes));
        if (recentSendCount >= _otpOptions.MaxSendAttemptsPerWindow)
        {
            throw new InvalidOperationException($"Too many OTP requests. Please wait {_otpOptions.SendWindowMinutes} minutes before trying again.");
        }

        if (existingOtps.Any(o => o.IsLocked))
        {
            throw new InvalidOperationException("Account is temporarily locked due to too many failed attempts. Please try again later.");
        }

        foreach (var old in existingOtps)
        {
            old.UsedAt = DateTime.UtcNow;
        }

        var otp = GenerateOtp();
        var otpHash = HashOtp(otp);
        var expiresAt = DateTime.UtcNow.AddMinutes(_otpOptions.ExpiryMinutes);

        await _dbContext.OtpRecords.AddAsync(new OtpRecord
        {
            UserId = userId,
            Email = email,
            OtpHash = otpHash,
            Type = type,
            ExpiresAt = expiresAt
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return otp;
    }

    private string BuildRegistrationEmailBody(string fullName, string otp)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; padding: 30px; border-radius: 8px;'>
        <h2 style='color: #2c3e50; margin-bottom: 20px;'>Email Verification - AI Study Hub</h2>
        <p>Dear {System.Net.WebUtility.HtmlEncode(fullName)},</p>
        <p>Thank you for registering with AI Study Hub. To complete your email verification, please use the following one-time verification code:</p>
        <div style='background-color: #ffffff; padding: 20px; border-radius: 6px; text-align: center; margin: 20px 0; border: 1px solid #e0e0e0;'>
            <span style='font-size: 28px; font-weight: bold; letter-spacing: 4px; color: #2c3e50;'>{otp}</span>
        </div>
        <p><strong>Important:</strong> This verification code will expire in <em>{_otpOptions.ExpiryMinutes} minutes</em>. Please enter it promptly to avoid expiration.</p>
        <hr style='border: none; border-top: 1px solid #e0e0e0; margin: 20px 0;'>
        <div style='background-color: #fff3cd; padding: 15px; border-radius: 6px; margin: 15px 0;'>
            <p style='margin: 0;'><strong>Security Notice:</strong></p>
            <p style='margin: 10px 0 0 0;'>If you did not initiate this account registration, please disregard this email. No further action is required on your part. Your email address will not be associated with any account without your explicit verification.</p>
        </div>
        <p>If you have any questions or require assistance, please contact our support team.</p>
        <p>Thank you for choosing AI Study Hub.</p>
        <p>Kind regards,<br><strong>AI Study Hub Support Team</strong></p>
    </div>
</body>
</html>";
    }

    private string BuildPasswordResetEmailBody(string fullName, string otp)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; padding: 30px; border-radius: 8px;'>
        <h2 style='color: #2c3e50; margin-bottom: 20px;'>Password Reset - AI Study Hub</h2>
        <p>Dear {System.Net.WebUtility.HtmlEncode(fullName)},</p>
        <p>We received a request to reset your AI Study Hub account password. Please use the following one-time verification code to proceed:</p>
        <div style='background-color: #ffffff; padding: 20px; border-radius: 6px; text-align: center; margin: 20px 0; border: 1px solid #e0e0e0;'>
            <span style='font-size: 28px; font-weight: bold; letter-spacing: 4px; color: #2c3e50;'>{otp}</span>
        </div>
        <p><strong>Important:</strong> This verification code will expire in <em>{_otpOptions.ExpiryMinutes} minutes</em>. Please enter it promptly to avoid expiration.</p>
        <hr style='border: none; border-top: 1px solid #e0e0e0; margin: 20px 0;'>
        <div style='background-color: #fff3cd; padding: 15px; border-radius: 6px; margin: 15px 0;'>
            <p style='margin: 0;'><strong>Security Notice:</strong></p>
            <p style='margin: 10px 0 0 0;'>If you did not request this password reset, please disregard this email. Your password will not be changed, and no further action is required on your part. We recommend that you keep your account secure by not sharing your password with anyone.</p>
        </div>
        <p>If you have any questions or require assistance, please contact our support team.</p>
        <p>Thank you for choosing AI Study Hub.</p>
        <p>Kind regards,<br><strong>AI Study Hub Support Team</strong></p>
    </div>
</body>
</html>";
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
