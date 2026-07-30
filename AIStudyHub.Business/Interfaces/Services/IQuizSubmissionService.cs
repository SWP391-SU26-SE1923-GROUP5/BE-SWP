using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.QuizSubmissions;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuizSubmissionService
{
    Task<IReadOnlyList<QuizSubmissionResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<QuizSubmissionDetailDto?> GetOwnedDetailAsync(Guid submissionId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuizSubmissionResponseDto>> GetByUserAndQuizAsync(Guid userId, Guid quizId, CancellationToken cancellationToken = default);
    Task<PagedResultDto<QuizSubmissionHistoryDto>> GetMyHistoryAsync(Guid userId, Guid? quizId, DateTime? fromDate, DateTime? toDate, PaginationParams @params, CancellationToken ct = default);
    Task<PagedResultDto<QuizSubmissionHistoryDto>> GetQuizHistoryAsync(Guid quizId, Guid userId, DateTime? fromDate, DateTime? toDate, PaginationParams @params, CancellationToken ct = default);
    Task<SubmitQuizResultDto> SubmitAsync(CreateQuizSubmissionRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
