using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Votes;

public sealed record VoteResponseDto(Guid Id, Guid UserId, Guid DocumentId, VoteType Type, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateVoteRequestDto(Guid DocumentId, VoteType Type);

public sealed record UpdateVoteRequestDto(VoteType Type);
