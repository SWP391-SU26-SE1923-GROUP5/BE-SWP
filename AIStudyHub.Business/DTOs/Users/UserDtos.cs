namespace AIStudyHub.Business.DTOs.Users;

public sealed record UserResponseDto(
    Guid Id,
    string FullName,
    string Email,
    DateOnly? DateOfBirth,
    int CurrentStorageCapacity,
    int CurrentAiTokenUsage,
    string Status,
    string Role,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateUserRequestDto(
    string FullName,
    string Email,
    string Password,
    DateOnly? DateOfBirth,
    int CurrentStorageCapacity,
    int CurrentAiTokenUsage,
    string Status,
    string Role);

public sealed record UpdateUserRequestDto(
    string FullName,
    DateOnly? DateOfBirth,
    int CurrentStorageCapacity,
    int CurrentAiTokenUsage,
    string Status,
    string Role);
