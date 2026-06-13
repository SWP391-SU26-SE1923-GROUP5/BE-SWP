namespace AIStudyHub.Business.DTOs.TierUsers;

public sealed record TierUserResponseDto(Guid Id, Guid UserId, Guid TierMembershipId, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateTierUserRequestDto(Guid UserId, Guid TierMembershipId);
