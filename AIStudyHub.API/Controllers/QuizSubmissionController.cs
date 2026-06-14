using AIStudyHub.Business.DTOs.QuizSubmissions;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class QuizSubmissionController : ControllerBase
{
    private readonly IQuizSubmissionService _service;

    public QuizSubmissionController(IQuizSubmissionService service)
    {
        _service = service;
    }

    /// <summary>Lấy tất cả kết quả nộp bài (Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<QuizSubmissionResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy kết quả nộp bài theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuizSubmissionResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // POST   /api/QuizSubmission  - Đã xóa. Nộp bài thi qua luồng nghiệp vụ Quiz riêng (Submit + Scoring).
    // PUT    /api/QuizSubmission/{id} - Đã xóa. Kết quả không được sửa sau khi nộp.
    // DELETE /api/QuizSubmission/{id} - Đã xóa. Kết quả không được xóa bởi người dùng.
}
