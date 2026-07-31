using AIStudyHub.Business.DTOs.Flashcards;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IFlashcardService : ICrudService<FlashcardResponseDto, CreateFlashcardRequestDto, UpdateFlashcardRequestDto>
{
    Task<IReadOnlyList<FlashcardResponseDto>> GetByDeckAsync(
        Guid deckId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> DeleteDeckAsync(
        Guid deckId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> DeleteByDocumentAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<FlashcardResponseDto>> GetAllPagedAsync(AIStudyHub.Business.DTOs.Common.PaginationParams @params, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FlashcardResponseDto>> CreateBulkAsync(IReadOnlyList<CreateFlashcardRequestDto> requests, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlashcardDeckSummaryDto>> GetDecksByDocumentAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
