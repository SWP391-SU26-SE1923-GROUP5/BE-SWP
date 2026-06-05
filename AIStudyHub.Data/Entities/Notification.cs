using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.System;
    public bool IsRead { get; set; }

    public User User { get; set; } = null!;
}
