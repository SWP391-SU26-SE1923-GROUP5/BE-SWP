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
    Guid TierId,
    string TierName,
    int TierStorageLimitMb,
    int TierAiTokens,
    DateTime? TierExpireAt,
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

public sealed record UpdateProfileRequestDto(
    string FullName,
    DateOnly? DateOfBirth);

public sealed record UpdateUserTierRequestDto(
    Guid TierId,
    DateTime? TierExpireAt);

public sealed record UserTierInfoDto(
    Guid TierId,
    string TierName,
    int StorageLimitMb,
    int AiTokens,
    DateTime? TierExpireAt,
    int CurrentStorageMb,
    int CurrentAiTokensUsed);

/// <summary>Lightweight projection of a user for share-target pickers (excludes the caller).</summary>
public sealed record ShareableUserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role);
