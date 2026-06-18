using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ReportController : ControllerBase
{
    private readonly IReportService _service;

    public ReportController(IReportService service)
    {
        _service = service;
    }

    /// <summary>Lấy tất cả báo cáo vi phạm (Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<ReportResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin báo cáo theo ID (Admin only).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ReportResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Gửi báo cáo vi phạm về một tài liệu.</summary>
    [HttpPost]
    public async Task<ActionResult<ReportResponseDto>> Create([FromBody] CreateReportRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // PUT    /api/Report/{id} - Đã xóa. Trạng thái report chỉ Admin mới được duyệt (endpoint riêng sau này).
    // DELETE /api/Report/{id} - Đã xóa. Người dùng không được xóa report đã gửi.
}
