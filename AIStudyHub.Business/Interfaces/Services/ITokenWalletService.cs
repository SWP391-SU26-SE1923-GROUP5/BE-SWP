using AIStudyHub.Business.DTOs.TokenWallet;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ITokenWalletService
{
    /// <summary>Reserve tokens before an AI operation. Throws QuotaExceededException if over limit.</summary>
    Task<TokenReservationDto> ReserveAsync(Guid userId, string operationType, int estimatedTokens, Guid? relatedEntityId, CancellationToken ct = default);

    /// <summary>Settle after AI operation completes. Refunds overage tokens.</summary>
    Task SettleAsync(Guid ledgerId, int actualTokens, CancellationToken ct = default);

    /// <summary>Refund on failure/cancellation.</summary>
    Task RefundAsync(Guid ledgerId, string reason, CancellationToken ct = default);

    /// <summary>Get current balance + recent reservations.</summary>
    Task<TokenWalletResponseDto> GetBalanceAsync(Guid userId, CancellationToken ct = default);
}
