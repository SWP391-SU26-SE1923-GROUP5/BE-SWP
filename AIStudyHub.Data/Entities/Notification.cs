using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public NotificationType Type { get; set; } = NotificationType.System;
    public User User { get; set; } = null!;
}
