namespace AIStudyHub.Data.Entities;

/// <summary>
/// Represents the assignment of a user to a membership tier.
/// </summary>
public sealed class TierUser : BaseEntity
{
    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the tier membership identifier.
    /// </summary>
    public Guid TierMembershipId { get; set; }

    /// <summary>
    /// Gets or sets the related user.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the related tier membership.
    /// </summary>
    public TierMembership TierMembership { get; set; } = null!;
}
