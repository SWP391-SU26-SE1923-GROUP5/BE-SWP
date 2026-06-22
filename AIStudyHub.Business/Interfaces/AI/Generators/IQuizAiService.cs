using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.DTOs.Quizzes;

namespace AIStudyHub.Business.Interfaces.AI.Generators;

public interface IQuizAiService
{
    /// <summary>
    /// Generate a quiz of the requested number of questions from a document's
    /// chunks. Persists the resulting Quiz/Question/Answer rows.
    /// </summary>
    Task<QuizResponseDto> GenerateAndPersistQuizAsync(
        Guid documentId,
        CreateQuizRequestViaAIDto request,
        Guid userId,
        CancellationToken cancellationToken = default);
}
