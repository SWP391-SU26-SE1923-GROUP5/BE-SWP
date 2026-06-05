using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Users;

public sealed record UserResponseDto(
    Guid Id,
    string FullName,
    string Email,
    DateOnly? DateOfBirth,
    Guid? TierId,
    int CurrentStorageCapacity,
    int CurrentAiToken,
    string Status,
    UserRole Role,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateUserRequestDto(
    string FullName,
    string Email,
    string Password,
    DateOnly? DateOfBirth,
    Guid? TierId,
    int CurrentStorageCapacity,
    int CurrentAiToken,
    string Status,
    UserRole Role);

public sealed record UpdateUserRequestDto(
    string FullName,
    DateOnly? DateOfBirth,
    Guid? TierId,
    int CurrentStorageCapacity,
    int CurrentAiToken,
    string Status,
    UserRole Role);
