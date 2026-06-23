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
    private readonly JwtOptions _jwtOptions;
    private readonly IEmailService _emailService;
    private readonly EmailVerificationOptions _emailVerificationOptions;
    private readonly OtpOptions _otpOptions;

    public AuthService(
        UserManager<User> userManager,
        ApplicationDbContext dbContext,
        IMapper mapper,
        JwtOptions jwtOptions,
        IEmailService emailService,
        EmailVerificationOptions emailVerificationOptions,
        OtpOptions otpOptions)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _mapper = mapper;
        _jwtOptions = jwtOptions;
        _emailService = emailService;
        _emailVerificationOptions = emailVerificationOptions;
        _otpOptions = otpOptions;
    }

    public async Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = BuildStudentUser(normalizedEmail, request.FullName, request.DateOfBirth);
        var createResult = await _userManager.CreateAsync(user, request.Password);
        EnsureIdentitySucceeded(createResult);

        await EnsureStudentRoleAsync(user);
        await SendEmailVerificationOtpAsync(user, cancellationToken);

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
        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            Console.WriteLine($"[DEBUG-AUTH] Refresh Token Failed: Cannot find token in Database!");
            Console.WriteLine($"[DEBUG-AUTH] Length of received string: {request.RefreshToken.Length}");
            Console.WriteLine($"[DEBUG-AUTH] Received string starts with: '{new string(request.RefreshToken.Take(15).ToArray())}...'");
            Console.WriteLine($"[DEBUG-AUTH] Received string ends with: '...{new string(request.RefreshToken.TakeLast(5).ToArray())}'");
            Console.WriteLine($"[DEBUG-AUTH] Calculated Hash: {tokenHash}");
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (!storedToken.IsActive)
        {
            Console.WriteLine($"[DEBUG-AUTH] Refresh Token Failed: Token found but IsActive is FALSE!");
            Console.WriteLine($"[DEBUG-AUTH] IsExpired: {storedToken.IsExpired} (ExpiresAt: {storedToken.ExpiresAt}, Now: {DateTime.UtcNow})");
            Console.WriteLine($"[DEBUG-AUTH] IsRevoked: {storedToken.IsRevoked} (RevokedAt: {storedToken.RevokedAt})");
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

            user = BuildStudentUser(normalizedEmail, fullName, null);
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
            throw new InvalidOperationException("Invalid or expired OTP.");
        }

        if (otpRecord.IsLocked)
        {
            throw new InvalidOperationException($"Too many failed attempts. Please wait {OtpRecord.LockoutMinutes} minutes before trying again.");
        }

        if (otpRecord.IsExpired)
        {
            throw new InvalidOperationException("OTP has expired. Please request a new one.");
        }

        if (!VerifyOtp(request.Otp, otpRecord.OtpHash))
        {
            otpRecord.FailedAttempts++;
            if (otpRecord.FailedAttempts >= OtpRecord.MaxFailedAttempts)
            {
                otpRecord.LockedUntil = DateTime.UtcNow.AddMinutes(OtpRecord.LockoutMinutes);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid or expired OTP.");
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

        var existingOtps = await _dbContext.OtpRecords
            .Where(o => o.Email == normalizedEmail && o.UserId == user.Id && o.Type == OtpType.EmailVerification && o.ExpiresAt > DateTime.UtcNow && o.UsedAt == null)
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
            UserId = user.Id,
            Email = normalizedEmail,
            OtpHash = otpHash,
            Type = OtpType.EmailVerification,
            ExpiresAt = expiresAt
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; padding: 30px; border-radius: 8px;'>
        <h2 style='color: #2c3e50; margin-bottom: 20px;'>Email Verification - AI Study Hub</h2>
        <p>Dear {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
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
        await _emailService.SendAsync(normalizedEmail, "AI Study Hub - Email Verification Code", htmlBody, cancellationToken);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            return;
        }

        var existingOtps = await _dbContext.OtpRecords
            .Where(o => o.Email == normalizedEmail && o.Type == OtpType.PasswordReset && o.ExpiresAt > DateTime.UtcNow && o.UsedAt == null)
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
            UserId = user.Id,
            Email = normalizedEmail,
            OtpHash = otpHash,
            Type = OtpType.PasswordReset,
            ExpiresAt = expiresAt
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; padding: 30px; border-radius: 8px;'>
        <h2 style='color: #2c3e50; margin-bottom: 20px;'>Password Reset - AI Study Hub</h2>
        <p>Dear {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
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
        await _emailService.SendAsync(normalizedEmail, "AI Study Hub - Password Reset OTP", htmlBody, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            throw new InvalidOperationException("Invalid or expired OTP.");
        }

        var otpRecord = await _dbContext.OtpRecords
            .Where(o => o.Email == normalizedEmail && o.UserId == user.Id && o.Type == OtpType.PasswordReset && o.UsedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpRecord is null)
        {
            throw new InvalidOperationException("Invalid or expired OTP.");
        }

        if (otpRecord.IsLocked)
        {
            throw new InvalidOperationException($"Too many failed attempts. Please wait {OtpRecord.LockoutMinutes} minutes before trying again.");
        }

        if (otpRecord.IsExpired)
        {
            throw new InvalidOperationException("OTP has expired. Please request a new one.");
        }

        if (!VerifyOtp(request.Otp, otpRecord.OtpHash))
        {
            otpRecord.FailedAttempts++;
            if (otpRecord.FailedAttempts >= OtpRecord.MaxFailedAttempts)
            {
                otpRecord.LockedUntil = DateTime.UtcNow.AddMinutes(OtpRecord.LockoutMinutes);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid or expired OTP.");
        }

        otpRecord.UsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        EnsureIdentitySucceeded(result);
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

    private static User BuildStudentUser(string normalizedEmail, string fullName, DateOnly? dateOfBirth)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            DateOfBirth = dateOfBirth,
            TierId = Guid.Parse("11111111-1111-1111-1111-111111111111"), // Free Tier
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
        var otp = GenerateOtp();
        var otpHash = HashOtp(otp);
        var expiresAt = DateTime.UtcNow.AddMinutes(_otpOptions.ExpiryMinutes);

        await _dbContext.OtpRecords.AddAsync(new OtpRecord
        {
            UserId = user.Id,
            Email = user.Email!,
            OtpHash = otpHash,
            Type = OtpType.EmailVerification,
            ExpiresAt = expiresAt
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; padding: 30px; border-radius: 8px;'>
        <h2 style='color: #2c3e50; margin-bottom: 20px;'>Email Verification - AI Study Hub</h2>
        <p>Dear {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
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
        await _emailService.SendAsync(user.Email!, "AI Study Hub - Email Verification Code", htmlBody, cancellationToken);
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
