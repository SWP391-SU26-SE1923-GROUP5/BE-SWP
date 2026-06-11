namespace AIStudyHub.Data.Entities;

public sealed class OtpRecord : BaseEntity
{
    public new Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public OtpType Type { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsed => UsedAt.HasValue;
    public bool IsLocked => LockedUntil.HasValue && DateTime.UtcNow < LockedUntil.Value;
    public bool IsValid => !IsExpired && !IsUsed && !IsLocked && FailedAttempts < MaxFailedAttempts;

    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 15;

    public User User { get; set; } = null!;
}

public enum OtpType
{
    PasswordReset,
    EmailVerification
}
