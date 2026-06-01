using AIStudyHub.Business.DTOs.Questions;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuestionService : ICrudService<QuestionResponseDto, CreateQuestionRequestDto, UpdateQuestionRequestDto>
{
}
