using AIStudyHub.Business.DTOs.Flashcards;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IFlashcardAiService
{
    Task<FlashcardsAiResponseDto> GenerateFlashcardsAsync(Guid documentId, CreateFlashcardsViaAiRequestDto request, Guid userId, CancellationToken cancellationToken = default);
}
