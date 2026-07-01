namespace AIStudyHub.Business.DTOs.TierMemberships;

public sealed record TierMembershipResponseDto(Guid Id, string TierName, decimal Price, int StorageLimitMb, int AiTokens, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateTierMembershipRequestDto(string TierName, decimal Price, int StorageLimitMb, int AiTokens);

public sealed record UpdateTierMembershipRequestDto(string TierName, decimal Price, int StorageLimitMb, int AiTokens);
