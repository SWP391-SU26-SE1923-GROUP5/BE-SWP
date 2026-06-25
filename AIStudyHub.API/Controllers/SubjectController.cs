using AIStudyHub.Business.DTOs.Subjects;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class SubjectController : ControllerBase
{
    private readonly ISubjectService _service;

    public SubjectController(ISubjectService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách tất cả môn học.</summary>
    [HttpGet]
    public async Task<ActionResult<AIStudyHub.Business.DTOs.Common.PagedResultDto<SubjectResponseDto>>> GetAll([FromQuery] AIStudyHub.Business.DTOs.Common.PaginationParams @params, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllPagedAsync(@params, cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin môn học theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubjectResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Tạo môn học mới (Admin only).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SubjectResponseDto>> Create([FromBody] CreateSubjectRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Cập nhật môn học (Admin only).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SubjectResponseDto>> Update(Guid id, [FromBody] UpdateSubjectRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Xóa môn học (Admin only).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
