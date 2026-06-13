using AIStudyHub.Business.DTOs.TierUsers;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TierUserController : ControllerBase
{
    private readonly ITierUserService _service;
    private readonly UserManager<User> _userManager;

    public TierUserController(ITierUserService service, UserManager<User> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    /// <summary>Lấy tất cả tier assignment (Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<TierUserResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy tier assignment theo ID (Admin only).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TierUserResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Lấy tier hiện tại của người dùng đang đăng nhập.</summary>
    [HttpGet("my")]
    public async Task<ActionResult<TierUserResponseDto>> GetMyTier(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await _service.GetActiveByUserIdAsync(user.Id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // POST   /api/TierUser  - Đã xóa. Việc gán tier phải đi qua luồng thanh toán (Payment).
    // DELETE /api/TierUser/{id} - Đã xóa. Hủy tier phải đi qua luồng nghiệp vụ riêng.
}
