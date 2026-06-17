using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class Payment : BaseEntity
{
    public Guid UserId { get; set; }
    public string PaymentInfo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public PaymentStatus? Status { get; set; } = PaymentStatus.Pending;
    public Guid? TierId { get; set; }
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;

    public User User { get; set; } = null!;
    public TierMembership? TierMembership { get; set; }
}
