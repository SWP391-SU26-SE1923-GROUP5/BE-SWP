using AIStudyHub.Business.DTOs.Questions;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class QuestionController : ControllerBase
{
    private readonly IQuestionService _service;

    public QuestionController(IQuestionService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách tất cả câu hỏi.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuestionResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin câu hỏi theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuestionResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // POST   /api/Question  - Đã xóa. Câu hỏi phải được tạo thông qua Quiz (AI generated).
    // PUT    /api/Question/{id} - Đã xóa.
    // DELETE /api/Question/{id} - Đã xóa. Xóa qua Quiz.
}
