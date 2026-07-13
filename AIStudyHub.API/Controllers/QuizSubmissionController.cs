using AIStudyHub.Business.DTOs.Common;
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

    /// <summary>Lấy lịch sử nộp bài của user hiện tại (Plan C3 / B.2.2).</summary>
    [HttpGet("my")]
    public async Task<ActionResult<PagedResultDto<QuizSubmissionHistoryDto>>> GetMyHistory(
        [FromQuery] Guid? quizId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var @params = new PaginationParams { Offset = offset, Limit = limit };
        var result = await _service.GetMyHistoryAsync(userId, quizId, fromDate, toDate, @params, ct);
        return Ok(result);
    }

    /// <summary>Lấy lịch sử nộp bài theo quiz (Plan C3 / B.2.2).</summary>
    [HttpGet("by-quiz/{quizId:guid}")]
    public async Task<ActionResult<IReadOnlyList<QuizSubmissionResponseDto>>> GetByQuiz(
        Guid quizId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _service.GetByUserAndQuizAsync(userId, quizId, ct);
        return Ok(result);
    }

    /// <summary>Lấy lịch sử nộp bài của tất cả user theo quiz.</summary>
    [HttpGet("quiz/{quizId:guid}/history")]
    public async Task<ActionResult<PagedResultDto<QuizSubmissionHistoryDto>>> GetQuizHistory(
        Guid quizId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var @params = new PaginationParams { Offset = offset, Limit = limit };
        var result = await _service.GetQuizHistoryAsync(quizId, fromDate, toDate, @params, ct);
        return Ok(result);
    }

    // POST   /api/QuizSubmission  - Đã xóa. Nộp bài thi qua luồng nghiệp vụ Quiz riêng (Submit + Scoring).
    // PUT    /api/QuizSubmission/{id} - Đã xóa. Kết quả không được sửa sau khi nộp.
    // DELETE /api/QuizSubmission/{id} - Đã xóa. Kết quả không được xóa bởi người dùng.

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
