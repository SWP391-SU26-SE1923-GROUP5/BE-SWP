using AIStudyHub.Business.DTOs.Quizzes;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuizAiService
{
<<<<<<< HEAD
    /// <summary>
    /// Generate a quiz of the requested number of questions from a document's
    /// chunks. Persists the resulting Quiz/Question/Answer rows.
    /// </summary>
    Task<AiGeneratedQuizResponseDto> GenerateAndPersistQuizAsync(
=======
    Task<AiGeneratedQuizResponseDto> GenerateQuizAsync(
>>>>>>> b2820b1166319b4413a27b83e4366c51cf8c1b80
        Guid documentId,
        CreateQuizRequestViaAIDto request,
        Guid userId,
        CancellationToken cancellationToken = default);
<<<<<<< HEAD
}
=======
}
>>>>>>> b2820b1166319b4413a27b83e4366c51cf8c1b80
