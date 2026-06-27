using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.AI.Generators;
using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class FlashcardController : ControllerBase
{
    private readonly IFlashcardService _service;
    private readonly IFlashcardAiService _flashcardAiService;
    private readonly IDocumentService _documentService;

    public FlashcardController(IFlashcardService service, IFlashcardAiService flashcardAiService, IDocumentService documentService)
    {
        _service = service;
        _flashcardAiService = flashcardAiService;
        _documentService = documentService;
    }

    [HttpPost("/api/flashcard/document/{docId:guid}/ai-gen")]
    public async Task<ActionResult<IReadOnlyList<FlashcardResponseDto>>> GenerateFromDocument(Guid docId, [FromBody] CreateFlashcardsViaAiRequestDto request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "userId")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        var aiResult = await _flashcardAiService.GenerateFlashcardsAsync(docId, request, userId, cancellationToken);
        return Ok(aiResult);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "userId")?.Value;
        return claim != null && Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }

    [HttpGet("/api/flashcard/document/{docId:guid}")]
    public async Task<ActionResult<IReadOnlyList<FlashcardResponseDto>>> GetByDocument(
        Guid docId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetByDocumentAsync(docId, userId, cancellationToken);
        return Ok(result);
    }



    /// <summary>Lấy danh sách tất cả flashcard.</summary>
    [HttpGet]
    public async Task<ActionResult<AIStudyHub.Business.DTOs.Common.PagedResultDto<FlashcardResponseDto>>> GetAll([FromQuery] AIStudyHub.Business.DTOs.Common.PaginationParams @params, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetAllPagedAsync(@params, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin flashcard theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FlashcardResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();

        var document = await _documentService.GetByIdAsync(result.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId && document.ShareStatus != "public") return Forbid();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<FlashcardResponseDto>> Create([FromBody] CreateFlashcardRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FlashcardResponseDto>> Update(Guid id, [FromBody] UpdateFlashcardRequestDto request, CancellationToken cancellationToken)
    {
        var flashcard = await _service.GetByIdAsync(id, cancellationToken);
        if (flashcard == null) return NotFound();

        var document = await _documentService.GetByIdAsync(flashcard.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId) return Forbid();

        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var flashcard = await _service.GetByIdAsync(id, cancellationToken);
        if (flashcard == null) return NotFound();

        var document = await _documentService.GetByIdAsync(flashcard.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId) return Forbid();

        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
