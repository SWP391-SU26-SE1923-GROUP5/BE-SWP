using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Features.Users;
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

    public UserController(IMediator mediator)
    {
        _mediator = mediator;
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

    // POST   /api/User  - Đã xóa. Dùng POST /api/Auth/register để tạo tài khoản qua luồng Identity + OTP.
    // PUT    /api/User/{id} - Đã xóa. Tính năng cập nhật profile sẽ được xử lý riêng.
    // DELETE /api/User/{id} - Đã xóa. Xóa user cần nghiệp vụ đặc thù (deactivate, cleanup data...).
}
