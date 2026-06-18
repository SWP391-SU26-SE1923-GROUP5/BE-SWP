using AIStudyHub.Business.DTOs.Votes;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class VoteController : ControllerBase
{
    private readonly IVoteService _service;

    public VoteController(IVoteService service)
    {
        _service = service;
    }

    /// <summary>Lấy thông tin vote theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VoteResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Thả vote cho một tài liệu.</summary>
    [HttpPost]
    public async Task<ActionResult<VoteResponseDto>> Create([FromBody] CreateVoteRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _service.CreateVoteAsync(userId, request.DocumentId, request.Type, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Rút vote (xóa vote của chính mình).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    // GET    /api/Vote  (GetAll) - Đã xóa. Vote là dữ liệu riêng tư, không liệt kê tất cả.
    // PUT    /api/Vote/{id} - Đã xóa. Vote không có khái niệm cập nhật, chỉ có thả hoặc rút.

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        return claim != null && Guid.TryParse(claim.Value, out var userId)
            ? userId
            : Guid.Empty;
    }
}
