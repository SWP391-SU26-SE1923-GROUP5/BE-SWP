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
        private readonly AIStudyHub.Business.Interfaces.Services.IFlashcardAiService _flashcardAiService;

    public FlashcardController(IFlashcardService service, AIStudyHub.Business.Interfaces.Services.IFlashcardAiService flashcardAiService)
    {
        _service = service;
        _flashcardAiService = flashcardAiService;
    }

    [HttpPost("/api/flashcard/document/{docId:guid}/ai-gen")]
    public async Task<ActionResult<AIStudyHub.Business.DTOs.Flashcards.FlashcardsAiResponseDto>> GenerateFromDocument(Guid docId, [FromBody] CreateFlashcardsViaAiRequestDto request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "userId")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        var result = await _flashcardAiService.GenerateFlashcardsAsync(docId, request, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy danh sách tất cả flashcard.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FlashcardResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin flashcard theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FlashcardResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
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
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    // POST   /api/Flashcard  - Đã xóa. Flashcard phải được tạo từ Document qua AI.
    // PUT    /api/Flashcard/{id} - Đã xóa.
    // DELETE /api/Flashcard/{id} - Đã xóa.
}
