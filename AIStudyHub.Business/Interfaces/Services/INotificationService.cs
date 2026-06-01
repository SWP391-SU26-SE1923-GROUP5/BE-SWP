using AIStudyHub.Business.DTOs.Notifications;

namespace AIStudyHub.Business.Interfaces.Services;

public interface INotificationService : ICrudService<NotificationResponseDto, CreateNotificationRequestDto, UpdateNotificationRequestDto>
{
}
