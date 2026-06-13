using AIStudyHub.Business.DTOs.Users;

namespace AIStudyHub.Business.DTOs.Authentication;

public sealed record RegisterRequestDto(
    string FullName,
    string Email,
    string Password,
    DateOnly? DateOfBirth);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record RefreshTokenRequestDto(string RefreshToken);

public sealed record AuthResponseDto(
    UserResponseDto User,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

public sealed record VerifyRegistrationOtpRequestDto(
    string Email,
    string Otp);
