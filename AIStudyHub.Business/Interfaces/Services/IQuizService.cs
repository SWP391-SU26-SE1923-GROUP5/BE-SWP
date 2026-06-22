using AIStudyHub.Business.DTOs.Quizzes;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IQuizService : ICrudService<QuizResponseDto, CreateQuizRequestDto, UpdateQuizRequestDto>
{
    Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<QuizResponseDto>> GetAllPagedAsync(AIStudyHub.Business.DTOs.Common.PaginationParams @params, CancellationToken cancellationToken = default);
}
