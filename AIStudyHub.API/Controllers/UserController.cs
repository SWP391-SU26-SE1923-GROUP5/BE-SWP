using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Features.Users.Commands;
using AIStudyHub.Business.Features.Users.Queries;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class UserController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserService _userService;

    public UserController(IMediator mediator, IUserService userService)
    {
        _mediator = mediator;
        _userService = userService;
    }

    /// <summary>Lấy danh sách tất cả người dùng (Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<UserResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUsersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin một người dùng theo ID (Admin only).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Lấy thông tin tier hiện tại của user đang đăng nhập.</summary>
    [HttpGet("me/tier")]
    public async Task<ActionResult<UserTierInfoDto>> GetMyTier(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _userService.GetUserTierInfoAsync(userId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Lấy danh sách người dùng có thể chia sẻ tài liệu (loại trừ người gọi).
    /// Hỗ trợ tìm kiếm theo tên hoặc email qua query <c>keyword</c>.
    /// </summary>
    [HttpGet("shareable")]
    public async Task<ActionResult<IReadOnlyList<ShareableUserDto>>> GetShareableUsers(
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _userService.GetShareableUsersAsync(userId, keyword, cancellationToken);
        return Ok(result);
    }

    /// <summary>Admin: Cập nhật tier của user.</summary>
    [HttpPut("{id:guid}/tier")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUserTier(Guid id, [FromBody] UpdateUserTierRequestDto request, CancellationToken cancellationToken)
    {
        await _userService.UpdateUserTierAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Người dùng tự cập nhật thông tin cá nhân (Profile).</summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        await _userService.UpdateProfileAsync(userId, request, cancellationToken);
        return NoContent();
    }

    // POST   /api/User  - Đã xóa. Dùng POST /api/Auth/register để tạo tài khoản qua luồng Identity + OTP.
    // DELETE /api/User/{id} - Đã xóa. Xóa user cần nghiệp vụ đặc thù (deactivate, cleanup data...).

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
