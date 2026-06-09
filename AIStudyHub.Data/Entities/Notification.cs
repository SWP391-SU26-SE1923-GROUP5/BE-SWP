namespace AIStudyHub.Data.Entities;

public sealed class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    public User User { get; set; } = null!;
}
