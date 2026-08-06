using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.DTOs.Quizzes;

namespace AIStudyHub.Business.Interfaces.AI.Generators;

public interface IQuizAiService
{
    /// <summary>
    /// Generates and persists exactly the requested number of questions from
    /// an owned, fully processed document.
    /// </summary>
    Task<QuizResponseDto> GenerateAndPersistQuizAsync(
        Guid documentId,
        CreateQuizRequestViaAiDto request,
        Guid userId,
        CancellationToken cancellationToken = default);
}
