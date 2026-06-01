using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
public sealed class NotificationController : CrudControllerBase<NotificationResponseDto, CreateNotificationRequestDto, UpdateNotificationRequestDto>
{
    public NotificationController(INotificationService service)
        : base(service)
    {
    }
}
