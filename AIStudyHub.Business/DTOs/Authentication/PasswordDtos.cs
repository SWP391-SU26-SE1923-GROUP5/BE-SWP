namespace AIStudyHub.Business.DTOs.Authentication;

public sealed record ForgotPasswordRequestDto(string Email);

public sealed record ResetPasswordRequestDto(
    string Email,
    string Otp,
    string NewPassword);

public sealed record ChangePasswordRequestDto(
    string CurrentPassword,
    string NewPassword);

public sealed record LogoutRequestDto(string RefreshToken);
