using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Flashcards;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IFlashcardService : ICrudService<FlashcardResponseDto, CreateFlashcardRequestDto, UpdateFlashcardRequestDto>
{
    Task<IReadOnlyList<FlashcardResponseDto>> GetByDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<FlashcardResponseDto>> GetAllPagedAsync(AIStudyHub.Business.DTOs.Common.PaginationParams @params, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FlashcardResponseDto>> CreateBulkAsync(IReadOnlyList<CreateFlashcardRequestDto> requests, CancellationToken cancellationToken = default);
}
