# Email Verification with OTP - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace token-based email verification with OTP flow.

**Architecture:** User registers with `EmailConfirmed = true`, receives OTP via email. Separate verification step sets `EmailConfirmed = true` after OTP validation. Login no longer requires email verification.

**Tech Stack:** ASP.NET Core, Entity Framework Core, ASP.NET Identity, MediatR

---

## Task 1: Add EmailVerification enum value

**Files:**
- Modify: `AIStudyHub.Data/Entities/OtpRecord.cs`

- [ ] **Step 1: Add EmailVerification to OtpType enum**

Modify `OtpRecord.cs` line 26-29:

```csharp
public enum OtpType
{
    PasswordReset,
    EmailVerification
}
```

- [ ] **Step 2: Commit**

```bash
git add AIStudyHub.Data/Entities/OtpRecord.cs
git commit -m "feat(auth): add EmailVerification otp type"
```

---

## Task 2: Update DTOs

**Files:**
- Modify: `AIStudyHub.Business/DTOs/Authentication/AuthDtos.cs`
- Modify: `AIStudyHub.Business/DTOs/Authentication/EmailVerificationDtos.cs`

- [ ] **Step 1: Add VerifyRegistrationOtpRequestDto to AuthDtos.cs**

Add to end of `AuthDtos.cs`:

```csharp
public sealed record VerifyRegistrationOtpRequestDto(
    string Email,
    string Otp);
```

- [ ] **Step 2: Remove old DTOs from EmailVerificationDtos.cs**

Replace entire `EmailVerificationDtos.cs` content:

```csharp
namespace AIStudyHub.Business.DTOs.Authentication;

public sealed record RegisterResultDto(
    string Message,
    string Email);
```

- [ ] **Step 3: Commit**

```bash
git add AIStudyHub.Business/DTOs/Authentication/AuthDtos.cs AIStudyHub.Business/DTOs/Authentication/EmailVerificationDtos.cs
git commit -m "refactor(auth): update DTOs for OTP verification"
```

---

## Task 3: Update IAuthService interface

**Files:**
- Modify: `AIStudyHub.Business/Interfaces/Services/IAuthService.cs`

- [ ] **Step 1: Remove old methods and add new one**

Replace interface content:

```csharp
using System.Security.Claims;
using AIStudyHub.Business.DTOs.Authentication;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IAuthService
{
    Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginExternalAsync(ExternalLoginRequestDto request, CancellationToken cancellationToken = default);
    Task VerifyRegistrationOtpAsync(VerifyRegistrationOtpRequestDto request, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ClaimsPrincipal userPrincipal, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
    Task LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Commit**

```bash
git add AIStudyHub.Business/Interfaces/Services/IAuthService.cs
git commit -m "refactor(auth): update IAuthService interface"
```

---

## Task 4: Update AuthService implementation

**Files:**
- Modify: `AIStudyHub.Business/Services/AuthService.cs`

- [ ] **Step 1: Update constructor - remove validators for removed methods**

Remove these fields from constructor:
```csharp
private readonly IValidator<ConfirmEmailRequestDto> _confirmEmailValidator;
private readonly IValidator<ResendEmailVerificationRequestDto> _resendEmailVerificationValidator;
```

Update constructor parameters (remove these two validators).

- [ ] **Step 2: Update RegisterAsync - set EmailConfirmed = true**

In `RegisterAsync` method (line 75), change:
```csharp
var user = BuildStudentUser(normalizedEmail, request.FullName, request.DateOfBirth, emailConfirmed: true);
```

Change the call to generate and send OTP instead of email verification link.

- [ ] **Step 3: Update LoginAsync - remove EmailConfirmed check**

Remove lines 97-100:
```csharp
if (!user.EmailConfirmed)
{
    throw new UnauthorizedAccessException("Email address has not been verified.");
}
```

- [ ] **Step 4: Add VerifyRegistrationOtpAsync method**

Add after `LoginExternalAsync` (around line 176):

```csharp
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
        .Where(o => o.Email == normalizedEmail && o.UserId == user.Id && o.Type == OtpType.EmailVerification && !o.IsUsed)
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
```

- [ ] **Step 5: Update ForgotPasswordAsync - use OtpType.PasswordReset (already correct)**

No changes needed.

- [ ] **Step 6: Remove SendEmailVerificationAsync method**

Delete the entire method (around lines 426-439).

- [ ] **Step 7: Update RegisterAsync to send OTP email**

Replace `SendEmailVerificationAsync` call with:

```csharp
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

var htmlBody = $"<p>Hello {System.Net.WebUtility.HtmlEncode(user.FullName)},</p><p>Your email verification OTP is: <strong>{otp}</strong></p><p>This code expires in {_otpOptions.ExpiryMinutes} minutes.</p><p>If you did not create this account, you can ignore this email.</p>";
await _emailService.SendAsync(normalizedEmail, "Verify your AIStudyHub email", htmlBody, cancellationToken);
```

- [ ] **Step 8: Commit**

```bash
git add AIStudyHub.Business/Services/AuthService.cs
git commit -m "feat(auth): implement OTP email verification flow"
```

---

## Task 5: Update AuthRequests (MediatR handlers)

**Files:**
- Modify: `AIStudyHub.Business/Features/Auth/AuthRequests.cs`

- [ ] **Step 1: Remove ConfirmEmailCommand and handler**

Delete these from lines 15 and 64-77:
```csharp
public sealed record ConfirmEmailCommand(ConfirmEmailRequestDto Request) : IRequest;

internal sealed class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand>
{ ... }
```

- [ ] **Step 2: Remove ResendEmailVerificationCommand and handler**

Delete these from lines 17 and 79-92:
```csharp
public sealed record ResendEmailVerificationCommand(ResendEmailVerificationRequestDto Request) : IRequest;

internal sealed class ResendEmailVerificationCommandHandler : IRequestHandler<ResendEmailVerificationCommand>
{ ... }
```

- [ ] **Step 3: Add VerifyRegistrationOtpCommand and handler**

Add after `RefreshTokenCommandHandler`:

```csharp
public sealed record VerifyRegistrationOtpCommand(VerifyRegistrationOtpRequestDto Request) : IRequest;

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
```

- [ ] **Step 4: Commit**

```bash
git add AIStudyHub.Business/Features/Auth/AuthRequests.cs
git commit -m "refactor(auth): update MediatR handlers for OTP verification"
```

---

## Task 6: Update AuthController

**Files:**
- Modify: `AIStudyHub.API/Controllers/AuthController.cs`

- [ ] **Step 1: Remove ConfirmEmail endpoint (lines 54-59)**

Delete:
```csharp
[HttpPost("confirm-email")]
public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequestDto request, CancellationToken cancellationToken)
{
    await _mediator.Send(new ConfirmEmailCommand(request), cancellationToken);
    return Ok(new { message = "Email verified successfully." });
}
```

- [ ] **Step 2: Remove ResendEmailVerification endpoint (lines 61-66)**

Delete:
```csharp
[HttpPost("resend-email-verification")]
public async Task<IActionResult> ResendEmailVerification(ResendEmailVerificationRequestDto request, CancellationToken cancellationToken)
{
    await _mediator.Send(new ResendEmailVerificationCommand(request), cancellationToken);
    return Ok(new { message = "If the account exists and is unverified, a verification email has been sent." });
}
```

- [ ] **Step 3: Add VerifyRegistrationOtp endpoint**

Add after `RefreshToken` endpoint:

```csharp
[HttpPost("verify-registration-otp")]
public async Task<IActionResult> VerifyRegistrationOtp(VerifyRegistrationOtpRequestDto request, CancellationToken cancellationToken)
{
    await _mediator.Send(new VerifyRegistrationOtpCommand(request), cancellationToken);
    return Ok(new { message = "Email verified successfully." });
}
```

- [ ] **Step 4: Commit**

```bash
git add AIStudyHub.API/Controllers/AuthController.cs
git commit -m "feat(auth): add verify-registration-otp endpoint"
```

---

## Task 7: Add database migration

**Files:**
- Create: `AIStudyHub.Data/Migrations/`

- [ ] **Step 1: Add migration for EmailVerification enum**

No migration needed - enum values in C# don't require EF migrations.

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded

- [ ] **Step 3: Commit (if changes needed)**

```bash
git add -A
git commit -m "chore: verify build after email verification changes"
```

---

## Verification

Run these commands to verify:

```bash
dotnet build
dotnet test
```

Expected: Build succeeded, all tests pass.
