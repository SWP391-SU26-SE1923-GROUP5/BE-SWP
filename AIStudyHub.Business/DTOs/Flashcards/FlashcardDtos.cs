using System;
using System.Collections.Generic;

namespace AIStudyHub.Business.DTOs.Flashcards;

public sealed record FlashcardResponseDto(Guid Id, Guid DocumentId, string Front, string Back, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateFlashcardRequestDto(Guid DocumentId, string Front, string Back);
public sealed record FlashcardResponseAiDto(string Front, string Back);
public sealed record UpdateFlashcardRequestDto(string Front, string Back);

public sealed record CreateFlashcardsViaAiRequestDto(int NumberOfFlashcards = 10);

public sealed record FlashcardsAiResponseDto(IReadOnlyList<FlashcardResponseAiDto> Flashcards);

public sealed record SaveGeneratedFlashcardsRequestDto(
    Guid DocumentId,
    IReadOnlyList<FlashcardResponseAiDto> Flashcards);
