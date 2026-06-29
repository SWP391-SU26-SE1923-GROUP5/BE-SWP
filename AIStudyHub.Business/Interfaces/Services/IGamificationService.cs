using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Gamification;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IGamificationService
{
    Task<ServiceResult<UserStatsResponseDto>> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<XpAwardResult>> AwardXpAsync(XpAwardRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboardAsync(
        int top,
        LeaderboardPeriod period = LeaderboardPeriod.AllTime,
        CancellationToken cancellationToken = default);
    Task EnsureUserStatsAsync(Guid userId, CancellationToken cancellationToken = default);
}
