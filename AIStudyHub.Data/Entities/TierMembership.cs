namespace AIStudyHub.Data.Entities;

/// <summary>
/// Represents a membership tier that defines platform usage limits.
/// </summary>
public sealed class TierMembership : BaseEntity
{
    /// <summary>
    /// Gets or sets the membership tier name.
    /// </summary>
    public string TierName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage limit in megabytes.
    /// </summary>
    public int StorageLimitMb { get; set; }

    /// <summary>
    /// Gets or sets the AI token allowance.
    /// </summary>
    public int AiTokens { get; set; }

    /// <summary>
    /// Gets or sets the users assigned to this tier.
    /// </summary>
    public ICollection<User> Users { get; set; } = [];

    /// <summary>
    /// Gets or sets the payments associated with this tier.
    /// </summary>
    public ICollection<Payment> Payments { get; set; } = [];
}
