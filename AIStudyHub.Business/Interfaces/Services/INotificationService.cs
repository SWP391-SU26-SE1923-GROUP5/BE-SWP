using AIStudyHub.Business.DTOs.Notifications;

namespace AIStudyHub.Business.Interfaces.Services;

public interface INotificationService : ICrudService<NotificationResponseDto, CreateNotificationRequestDto, UpdateNotificationRequestDto>
{
    Task<IReadOnlyList<NotificationResponseDto>> GetUserNotificationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
