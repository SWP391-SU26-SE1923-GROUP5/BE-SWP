namespace AIStudyHub.Data.Options;

public sealed class AdminSeedOptions
{
    public bool Enabled { get; init; } = true;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
