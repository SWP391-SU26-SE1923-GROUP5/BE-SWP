using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.AI.Generators;
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
    private readonly IDocumentService _documentService;

    public QuizController(IQuizService service, IDocumentService documentService)
    {
        _service = service;
        _documentService = documentService;
    }

    /// <summary>Lấy danh sách tất cả quiz.</summary>
    [HttpGet]
    public async Task<ActionResult<AIStudyHub.Business.DTOs.Common.PagedResultDto<QuizResponseDto>>> GetAll([FromQuery] AIStudyHub.Business.DTOs.Common.PaginationParams @params, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetAllPagedAsync(@params, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/quiz/document/{docId:guid}/ai-gen")]
    public async Task<ActionResult<QuizResponseDto>> GenerateFromDocument(
        Guid docId,
        [FromBody] CreateQuizRequestViaAIDto dto,
        [FromServices] IQuizAiService quizAiService,
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

            var fullQuiz = await _service.GetByIdAsync(result.Id, cancellationToken);
            return Ok(fullQuiz);
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
        if (result == null) return NotFound();

        var document = await _documentService.GetByIdAsync(result.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId && document.ShareStatus != "public") return Forbid();

        return Ok(result);
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
        var quiz = await _service.GetByIdAsync(id, cancellationToken);
        if (quiz == null) return NotFound();

        var document = await _documentService.GetByIdAsync(quiz.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId) return Forbid();

        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var quiz = await _service.GetByIdAsync(id, cancellationToken);
        if (quiz == null) return NotFound();

        var document = await _documentService.GetByIdAsync(quiz.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId) return Forbid();

        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "userId")?.Value;
        return claim != null && Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }

    // POST   /api/Quiz  - Đã xóa. Quiz phải được AI sinh ra từ Document.
    // PUT    /api/Quiz/{id} - Đã xóa.
    // DELETE /api/Quiz/{id} - Đã xóa. Xóa quiz phải đi kèm xóa Question và Answer con.
}
