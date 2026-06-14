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

    // POST   /api/Answer  - Đã xóa. Câu trả lời phải được tạo thông qua Question (AI generated).
    // PUT    /api/Answer/{id} - Đã xóa.
    // DELETE /api/Answer/{id} - Đã xóa.
}
