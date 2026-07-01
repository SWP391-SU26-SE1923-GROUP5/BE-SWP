namespace AIStudyHub.Business.DTOs.Notifications;

public sealed record NotificationResponseDto(Guid Id, Guid UserId, string Message, bool IsRead, string Type, DateTime CreatedAt, DateTime? UpdatedAt);

/// <summary>
/// Spec v4.0 / Module 3: response for the mark-as-read endpoints so the frontend
/// can immediately drop the badge counter without an extra round-trip
/// (replaces plain <c>204 NoContent</c>).
/// </summary>
public sealed record MarkAsReadResponseDto(bool Success, int UnreadCount);
