# Email Verification with OTP - Design Spec

**Date:** 2026-06-11
**Status:** Approved

## Overview

Thay thế email verification flow hiện tại (sử dụng token-based link) bằng OTP qua email. User đăng ký xong có thể login ngay, nhưng trường `EmailConfirmed` được set `true` khi user verify OTP thành công.

## Current State

- `POST /register` → tạo user với `EmailConfirmed = false`, gửi email kèm verification link (token-based)
- `POST /confirm-email` → verify bằng token, set `EmailConfirmed = true`
- `POST /login` → block login nếu `EmailConfirmed = false`
- `OtpRecord` entity đã tồn tại, dùng cho password reset

## New Flow

```
POST /register
  ├── Tạo user với EmailConfirmed = true
  ├── Gửi email kèm OTP (6 số)
  └── Response: "Registration successful. Please verify your email."

POST /verify-registration-otp
  ├── Nhận email + otp
  ├── Verify OTP → set EmailConfirmed = true
  └── Response: "Email verified successfully."

POST /login
  └── Hoạt động bình thường (không còn check EmailConfirmed)
```

## Removed Endpoints

- `POST /confirm-email` - xoá (token-based verification)
- `POST /resend-email-verification` - xoá

## Changes

### 1. Database

**`OtpRecord.cs`** - Thêm enum value:
```csharp
public enum OtpType
{
    PasswordReset,
    EmailVerification  // ← mới
}
```

### 2. API Endpoints (`AuthController.cs`)

**Xoá:**
- `POST /confirm-email`
- `POST /resend-email-verification`

**Thêm:**
- `POST /verify-registration-otp`

### 3. DTOs

**Thêm `AuthDtos.cs`:**
```csharp
public sealed record VerifyRegistrationOtpRequestDto(
    string Email,
    string Otp);
```

### 4. AuthService Changes

**`RegisterAsync`:**
- Tạo user với `EmailConfirmed = true`
- Gửi email kèm OTP thay vì verification link

**`LoginAsync`:**
- Xoá check `if (!user.EmailConfirmed)`

**`SendEmailVerificationAsync`:** xoá toàn bộ

**Thêm `VerifyRegistrationOtpAsync`:**
- Tìm OTP record hợp lệ
- Verify OTP (reused logic từ `ResetPasswordAsync`)
- Set `user.EmailConfirmed = true`
- Đánh dấu OTP đã used

### 5. Files to Modify

| File | Action |
|------|--------|
| `AuthController.cs` | Xoá 2 endpoints, thêm 1 endpoint |
| `IAuthService.cs` | Xoá 2 method signatures, thêm 1 |
| `AuthService.cs` | Refactor register, login, add verify method |
| `AuthRequests.cs` | Xoá 2 command/handler, thêm 1 |
| `AuthDtos.cs` | Thêm `VerifyRegistrationOtpRequestDto` |
| `EmailVerificationDtos.cs` | Xoá `ConfirmEmailRequestDto`, `ResendEmailVerificationRequestDto` |
| `OtpRecord.cs` | Thêm `EmailVerification` enum value |

## Security

- OTP hash bằng SHA256 (giống password reset)
- Timing-safe comparison
- Rate limiting: tối đa 3 OTP gửi trong 15 phút
- Lockout 15 phút sau 5 lần sai
- OTP expire sau 10 phút

## Error Handling

| Scenario | Response |
|----------|----------|
| OTP sai | `Invalid or expired OTP` |
| OTP expired | `OTP has expired` |
| OTP đã used | `Invalid or expired OTP` |
| Account locked | `Account is temporarily locked` |
| Email không tồn tại | `Invalid email verification request` |

## Testing

1. **Register → verify OTP → login**: User verify OTP thành công, `EmailConfirmed = true`, login được
2. **Register → login without verify**: Vẫn login được (vì `EmailConfirmed = true` từ đầu)
3. **Wrong OTP**: Verify thất bại, `EmailConfirmed` không đổi
4. **Expired OTP**: Verify thất bại
5. **Rate limiting**: Không gửi quá 3 OTP trong 15 phút
