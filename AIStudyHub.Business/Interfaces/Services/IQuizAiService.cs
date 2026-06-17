using AIStudyHub.Business.DTOs.Quizzes;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuizAiService
{
    Task<AiGeneratedQuizResponseDto> GenerateQuizAsync(
        Guid documentId,
        CreateQuizRequestViaAIDto request,
        Guid userId,
        CancellationToken cancellationToken = default);
}
