namespace AIStudyHub.Business.Options;

public sealed class OtpOptions
{
    public int CodeLength { get; init; } = 6;
    public int ExpiryMinutes { get; init; } = 3;
    public int MaxFailedAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
    public int MaxSendAttemptsPerWindow { get; init; } = 1;
    public int SendWindowMinutes { get; init; } = 3;
}
