using AIStudyHub.Business.DTOs.Answers;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class AnswerController : ControllerBase
{
    private readonly IAnswerService _service;

    public AnswerController(IAnswerService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách tất cả câu trả lời.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AnswerResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin câu trả lời theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnswerResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<AnswerResponseDto>> Create([FromBody] CreateAnswerRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AnswerResponseDto>> Update(Guid id, [FromBody] UpdateAnswerRequestDto request, CancellationToken cancellationToken)
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

    // POST   /api/Answer  - Đã xóa. Câu trả lời phải được tạo thông qua Question (AI generated).
    // PUT    /api/Answer/{id} - Đã xóa.
    // DELETE /api/Answer/{id} - Đã xóa.
}
