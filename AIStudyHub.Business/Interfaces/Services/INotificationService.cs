using AIStudyHub.Business.DTOs.Notifications;

namespace AIStudyHub.Business.Interfaces.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<NotificationResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationResponseDto>> GetUserNotificationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
