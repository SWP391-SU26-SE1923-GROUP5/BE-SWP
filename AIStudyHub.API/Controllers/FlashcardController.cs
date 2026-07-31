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
    private readonly IDocumentService _documentService;

    public FlashcardController(IFlashcardService service, IDocumentService documentService)
    {
        _service = service;
        _documentService = documentService;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "userId")?.Value;
        return claim != null && Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }

    [HttpGet("{deckId:guid}/flashcards")]
    public async Task<ActionResult<IReadOnlyList<FlashcardResponseDto>>> GetByDeck(
        Guid deckId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetByDeckAsync(deckId, userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<AIStudyHub.Business.DTOs.Common.PagedResultDto<FlashcardResponseDto>>> GetAll([FromQuery] AIStudyHub.Business.DTOs.Common.PaginationParams @params, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetAllPagedAsync(@params, userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FlashcardResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();

        var document = await _documentService.GetByIdAsync(result.DeckId, cancellationToken);
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

        var document = await _documentService.GetByIdAsync(flashcard.DeckId, cancellationToken);
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

        var document = await _documentService.GetByIdAsync(flashcard.DeckId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId) return Forbid();

        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("by-deck/{deckId:guid}")]
    public async Task<IActionResult> DeleteDeck(
        Guid deckId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _service.DeleteDeckAsync(deckId, userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("document/{documentId:guid}")]
    public async Task<IActionResult> DeleteByDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var count = await _service.DeleteByDocumentAsync(documentId, userId, cancellationToken);
        return Ok(new { deletedCount = count });
    }

    /// <summary>Get all flashcard decks belonging to a document.</summary>
    [HttpGet("document/{documentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<FlashcardDeckSummaryDto>>> GetDecksByDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var decks = await _service.GetDecksByDocumentAsync(documentId, userId, cancellationToken);
        return Ok(decks);
    }
}
