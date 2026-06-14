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

    public FlashcardController(IFlashcardService service)
    {
        _service = service;
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
