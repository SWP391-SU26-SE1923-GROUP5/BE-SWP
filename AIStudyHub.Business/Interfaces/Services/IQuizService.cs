using AIStudyHub.Business.DTOs.Quizzes;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuizService : ICrudService<QuizResponseDto, CreateQuizRequestDto, UpdateQuizRequestDto>
{
}
