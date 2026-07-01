using AIStudyHub.Business.DTOs.QuizSubmissions;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuizSubmissionService
{
    Task<IReadOnlyList<QuizSubmissionResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<QuizSubmissionResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuizSubmissionResponseDto>> GetByUserAndQuizAsync(Guid userId, Guid quizId, CancellationToken cancellationToken = default);
    Task<SubmitQuizResultDto> SubmitAsync(CreateQuizSubmissionRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
