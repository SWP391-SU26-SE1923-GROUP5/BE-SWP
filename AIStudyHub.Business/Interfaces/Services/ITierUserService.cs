using AIStudyHub.Business.DTOs.TierUsers;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ITierUserService
{
    Task<IReadOnlyList<TierUserResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TierUserResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TierUserResponseDto> CreateAsync(CreateTierUserRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TierUserResponseDto?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
