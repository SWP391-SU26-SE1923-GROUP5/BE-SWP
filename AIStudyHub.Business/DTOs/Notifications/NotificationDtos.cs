namespace AIStudyHub.Business.DTOs.Notifications;

public sealed record NotificationResponseDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    string? PayloadJson,
    string? ActionUrl,
    bool IsRead,
    string Type,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record MarkAsReadResponseDto(bool Success, int UnreadCount);
