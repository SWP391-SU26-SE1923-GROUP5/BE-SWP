namespace AIStudyHub.Business.DTOs.TokenWallet;

public sealed record TokenBalanceDto(
    int CurrentUsage,
    int MaxQuota,
    int Available,
    int TierAiTokens);

public sealed record TokenReservationDto(
    Guid LedgerId,
    Guid UserId,
    string OperationType,
    int EstimatedTokens,
    DateTime ReservedAt);

public sealed record TokenWalletHistoryDto(
    Guid LedgerId,
    string OperationType,
    string Status,
    int EstimatedTokens,
    int? ActualTokens,
    string? FailureReason,
    DateTime CreatedAt);

public sealed record TokenWalletResponseDto(
    TokenBalanceDto Balance,
    IReadOnlyList<TokenWalletHistoryDto> RecentReservations);
