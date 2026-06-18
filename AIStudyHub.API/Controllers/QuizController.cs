using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class QuizController : ControllerBase
{
    private readonly IQuizService _service;

    public QuizController(IQuizService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách tất cả quiz.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuizResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/quiz/document/{docId:guid}/ai-gen")]
    public async Task<ActionResult<AiGeneratedQuizResponseDto>> GenerateFromDocument(
        Guid docId,
        [FromBody] CreateQuizRequestViaAIDto dto,
        [FromServices] AIStudyHub.Business.Interfaces.Services.IQuizAiService quizAiService,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "userId")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        if (dto.numberOfQuestions <= 0 || dto.numberOfQuestions > 20)
            return BadRequest("Number of questions must be between 1 and 20.");

        try
        {
            var result = await quizAiService.GenerateAndPersistQuizAsync(
                docId, dto, userId, cancellationToken);

            if (result.Questions is null || result.Questions.Count == 0)
                return BadRequest("AI did not return any valid questions.");

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    /// <summary>Lấy thông tin quiz theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuizResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<QuizResponseDto>> Create([FromBody] CreateQuizRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuizResponseDto>> Update(Guid id, [FromBody] UpdateQuizRequestDto request, CancellationToken cancellationToken)
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

    // POST   /api/Quiz  - Đã xóa. Quiz phải được AI sinh ra từ Document.
    // PUT    /api/Quiz/{id} - Đã xóa.
    // DELETE /api/Quiz/{id} - Đã xóa. Xóa quiz phải đi kèm xóa Question và Answer con.
}
