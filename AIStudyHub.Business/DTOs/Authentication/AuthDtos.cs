using AIStudyHub.Business.Enums;

namespace AIStudyHub.Business.DTOs.Authentication;

public sealed record RegisterRequestDto(string FullName, string Email, string Password);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record AuthResponseDto(Guid UserId, string FullName, string Email, UserRole Role, string AccessToken, DateTime ExpiresAt);
