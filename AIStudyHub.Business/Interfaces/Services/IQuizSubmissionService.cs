using AIStudyHub.Business.DTOs.QuizSubmissions;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuizSubmissionService : ICrudService<QuizSubmissionResponseDto, CreateQuizSubmissionRequestDto, UpdateQuizSubmissionRequestDto>
{
}
