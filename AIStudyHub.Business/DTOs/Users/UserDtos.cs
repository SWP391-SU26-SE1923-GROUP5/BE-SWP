using AIStudyHub.Business.Enums;

namespace AIStudyHub.Business.DTOs.Users;

public sealed record UserResponseDto(Guid Id, string FullName, string Email, UserRole Role, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateUserRequestDto(string FullName, string Email, string Password, UserRole Role);

public sealed record UpdateUserRequestDto(string FullName, UserRole Role, bool IsActive);
