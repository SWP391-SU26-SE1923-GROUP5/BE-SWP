using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>Lấy thông tin quiz theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuizResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // POST   /api/Quiz  - Đã xóa. Quiz phải được AI sinh ra từ Document.
    // PUT    /api/Quiz/{id} - Đã xóa.
    // DELETE /api/Quiz/{id} - Đã xóa. Xóa quiz phải đi kèm xóa Question và Answer con.
}
