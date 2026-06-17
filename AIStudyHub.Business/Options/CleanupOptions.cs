namespace AIStudyHub.Business.Options;

public sealed class CleanupOptions
{
    public int UnverifiedAccountRetentionDays { get; init; } = 7;
    public int TierExpirationCheckIntervalHours { get; init; } = 24;
}
