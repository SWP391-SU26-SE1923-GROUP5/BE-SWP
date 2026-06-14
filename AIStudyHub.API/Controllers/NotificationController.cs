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

    // POST   /api/Notification - Đã xóa. Notification do hệ thống tạo ra (system-generated), không phải client.
    // PUT    /api/Notification/{id} - Đã xóa. Chỉ cần mark-as-read, sẽ có endpoint riêng.
    // DELETE /api/Notification/{id} - Đã xóa.
}
