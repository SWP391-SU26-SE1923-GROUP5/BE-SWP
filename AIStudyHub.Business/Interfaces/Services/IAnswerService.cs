using AIStudyHub.Business.DTOs.Answers;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IAnswerService
{
    Task<IReadOnlyList<AnswerResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AnswerResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnswerResponseDto>> GetByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
