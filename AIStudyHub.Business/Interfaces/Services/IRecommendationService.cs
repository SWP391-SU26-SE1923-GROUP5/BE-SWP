using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Recommendations;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IRecommendationService
{
    Task<ServiceResult<IReadOnlyList<SubjectMasteryDto>>> GetSubjectMasteryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<RecommendationResultDto>> GetRecommendationsAsync(Guid userId, CancellationToken cancellationToken = default);
}
