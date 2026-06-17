namespace AIStudyHub.Business.DTOs.Notifications;

public sealed record NotificationResponseDto(Guid Id, Guid UserId, string Message, bool IsRead, string Type, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateNotificationRequestDto(Guid UserId, string Message, string Type);

public sealed record UpdateNotificationRequestDto(string Message, bool IsRead);
