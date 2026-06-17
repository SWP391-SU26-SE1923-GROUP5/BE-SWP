using AIStudyHub.Business.DTOs.Flashcards;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IFlashcardService : ICrudService<FlashcardResponseDto, CreateFlashcardRequestDto, UpdateFlashcardRequestDto>
{
    Task<IReadOnlyList<FlashcardResponseDto>> CreateBulkAsync(IReadOnlyList<CreateFlashcardRequestDto> requests, CancellationToken cancellationToken = default);
}
