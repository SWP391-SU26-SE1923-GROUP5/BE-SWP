namespace AIStudyHub.Business.DTOs.Authentication;

public sealed record ForgotPasswordRequestDto(string Email);

public sealed record VerifyPasswordResetOtpRequestDto(string Email, string Otp);

public sealed record ConfirmResetPasswordRequestDto(string Email, string NewPassword);

public sealed record ChangePasswordRequestDto(
    string CurrentPassword,
    string NewPassword);

public sealed record LogoutRequestDto(string RefreshToken);
