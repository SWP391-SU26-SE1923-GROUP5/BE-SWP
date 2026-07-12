using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class NotificationController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationController(INotificationService service)
    {
        _service = service;
    }

    /// <summary>Lấy tất cả thông báo (Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<NotificationResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông báo theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Lấy thông báo của user hiện tại.</summary>
    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<NotificationResponseDto>>> GetMyNotifications(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _service.GetUserNotificationsAsync(userId, cancellationToken);

        // ETag: hash of unread count for cache invalidation
        var unreadCount = result.Count(n => !n.IsRead);
        var etag = $"\"{unreadCount}:{result.Count}\"";
        Response.Headers["ETag"] = etag;

        return Ok(result);
    }

    /// <summary>Đánh dấu một thông báo đã đọc. Trả về unreadCount mới nhất để UI giảm badge đỏ ngay.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<MarkAsReadResponseDto>> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        await _service.MarkAsReadAsync(id, cancellationToken);
        var unreadCount = await _service.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new MarkAsReadResponseDto(true, unreadCount));
    }

    /// <summary>Đánh dấu tất cả thông báo đã đọc. Trả về unreadCount mới nhất (thường = 0).</summary>
    [HttpPost("mark-all-read")]
    public async Task<ActionResult<MarkAsReadResponseDto>> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _service.MarkAllAsReadAsync(userId, cancellationToken);
        var unreadCount = await _service.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new MarkAsReadResponseDto(true, unreadCount));
    }

    /// <summary>Plan C4 / B.4.3 — đánh dấu một thông báo đã đọc (PUT semantic).</summary>
    [HttpPut("{id:guid}/read")]
    public async Task<ActionResult<MarkAsReadResponseDto>> MarkAsReadPut(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        await _service.MarkAsReadAsync(id, cancellationToken);
        var unreadCount = await _service.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new MarkAsReadResponseDto(true, unreadCount));
    }

    /// <summary>Plan C4 / B.4.3 — đánh dấu tất cả thông báo đã đọc (PUT semantic).</summary>
    [HttpPut("read-all")]
    public async Task<ActionResult<MarkAsReadResponseDto>> MarkAllAsReadPut(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _service.MarkAllAsReadAsync(userId, cancellationToken);
        var unreadCount = await _service.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new MarkAsReadResponseDto(true, unreadCount));
    }

    // POST   /api/Notification - Đã xóa. Notification do hệ thống tạo ra (system-generated), không phải client.
    // PUT    /api/Notification/{id} - Đã xóa. Chỉ cần mark-as-read, sẽ có endpoint riêng.
    // DELETE /api/Notification/{id} - Đã xóa.

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
