using AIStudyHub.Business.DTOs.Votes;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IVoteService
{
    Task<IReadOnlyList<VoteResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<VoteResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VoteResponseDto> CreateVoteAsync(Guid userId, Guid documentId, VoteType type, CancellationToken cancellationToken = default);
    Task<VoteResponseDto?> GetByUserAndDocumentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
