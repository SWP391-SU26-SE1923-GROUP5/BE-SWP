using AIStudyHub.Business.Enums;

namespace AIStudyHub.Business.DTOs.Notifications;

public sealed record NotificationResponseDto(Guid Id, Guid UserId, string Title, string Message, NotificationType Type, bool IsRead, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateNotificationRequestDto(Guid UserId, string Title, string Message, NotificationType Type);

public sealed record UpdateNotificationRequestDto(string Title, string Message, NotificationType Type, bool IsRead);
