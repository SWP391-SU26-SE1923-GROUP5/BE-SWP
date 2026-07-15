namespace AIStudyHub.Business.Options;

public sealed class DocumentReindexOptions
{
    public const string SectionName = "DocumentReindex";
    public bool Enabled { get; init; } = true;
    public int BatchSize { get; init; } = 10;
    public int ScanIntervalMinutes { get; init; } = 15;
    public int ClaimTimeoutMinutes { get; init; } = 30;
    public int MaxAttempts { get; init; } = 3;
}
