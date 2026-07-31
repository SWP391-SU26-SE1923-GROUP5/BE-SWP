using AIStudyHub.Business.DTOs.Flashcards;

namespace AIStudyHub.Business.Interfaces.AI.Generators;

public interface IFlashcardAiService
{
    Task<FlashcardDeckResponseDto> GenerateFlashcardsAsync(
        Guid documentId,
        CreateFlashcardsViaAiRequestDto request,
        Guid userId,
        CancellationToken cancellationToken = default);
}