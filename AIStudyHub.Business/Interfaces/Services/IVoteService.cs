using AIStudyHub.Business.DTOs.Votes;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IVoteService : ICrudService<VoteResponseDto, CreateVoteRequestDto, UpdateVoteRequestDto>
{
    Task<VoteResponseDto> CreateVoteAsync(Guid userId, Guid documentId, VoteType type, CancellationToken cancellationToken = default);
}
