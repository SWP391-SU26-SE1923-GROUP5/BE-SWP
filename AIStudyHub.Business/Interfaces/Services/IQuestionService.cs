using AIStudyHub.Business.DTOs.Questions;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuestionService : ICrudService<QuestionResponseDto, CreateQuestionRequestDto, UpdateQuestionRequestDto>
{
    Task<IReadOnlyList<QuestionResponseDto>> GetByQuizIdAsync(Guid quizId, CancellationToken cancellationToken = default);
}
