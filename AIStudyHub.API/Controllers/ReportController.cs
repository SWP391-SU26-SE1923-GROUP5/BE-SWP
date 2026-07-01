using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var userId) ? userId : Guid.Empty;
    }

    [HttpPost]
    public async Task<ActionResult<ReportResponseDto>> Create([FromBody] CreateReportRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _service.CreateWithUserIdAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("my-reports")]
    public async Task<ActionResult<IReadOnlyList<ReportResponseDto>>> GetMyReports(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _service.GetMyReportsAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();

        // Security check: Only Admin or the reporter can view
        var userId = GetUserId();
        if (result.UserId != userId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        return Ok(result);
    }

    [HttpGet("search")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResultDto<ReportResponseDto>>> Search([FromQuery] ReportFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ReportResponseDto>> UpdateStatus(Guid id, [FromBody] UpdateReportStatusRequestDto request, CancellationToken cancellationToken)
    {
        var adminId = GetUserId();
        var result = await _service.UpdateStatusAsync(id, request.Status, adminId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("bulk-status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BulkReportStatusResultDto>> BulkUpdateStatus([FromBody] BulkReportIdsRequestDto request, CancellationToken cancellationToken)
    {
        var adminId = GetUserId();
        var result = await _service.BulkUpdateStatusAsync(request.ReportIds, request.Status, adminId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("documents/{documentId:guid}/mark-non-flaggable")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> MarkDocumentNonFlaggable(Guid documentId, CancellationToken cancellationToken)
    {
        var adminId = GetUserId();
        var rejectedCount = await _service.MarkDocumentNonFlaggableAsync(documentId, adminId, cancellationToken);
        return Ok(new { message = $"Document marked as NonFlaggable. {rejectedCount} pending/reviewed reports were automatically rejected." });
    }

    [HttpPost("documents/bulk-mark-non-flaggable")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BulkMarkNonFlaggableResultDto>> BulkMarkNonFlaggable([FromBody] BulkDocumentIdsRequestDto request, CancellationToken cancellationToken)
    {
        var adminId = GetUserId();
        var result = await _service.BulkMarkNonFlaggableAsync(request.DocumentIds, adminId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
