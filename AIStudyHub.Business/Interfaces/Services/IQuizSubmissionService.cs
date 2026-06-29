using AIStudyHub.Business.DTOs.QuizSubmissions;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuizSubmissionService : ICrudService<QuizSubmissionResponseDto, CreateQuizSubmissionRequestDto, UpdateQuizSubmissionRequestDto>
{
    Task<IReadOnlyList<QuizSubmissionResponseDto>> GetByUserAndQuizAsync(Guid userId, Guid quizId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plan C3 / B.4.5 — submit a quiz attempt. Returns the saved submission plus
    /// any badges the user just unlocked so the frontend can celebrate inline.
    /// </summary>
    Task<SubmitQuizResultDto> SubmitAsync(CreateQuizSubmissionRequestDto request, CancellationToken cancellationToken = default);
}
