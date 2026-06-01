using AIStudyHub.Business.DTOs.Answers;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IAnswerService : ICrudService<AnswerResponseDto, CreateAnswerRequestDto, UpdateAnswerRequestDto>
{
}
