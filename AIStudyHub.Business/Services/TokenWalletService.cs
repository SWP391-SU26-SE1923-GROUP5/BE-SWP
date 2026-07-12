using AIStudyHub.Business.DTOs.TokenWallet;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed class TokenWalletService : ITokenWalletService
{
    private readonly IUnitOfWork _unitOfWork;

    public TokenWalletService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TokenReservationDto> ReserveAsync(Guid userId, string operationType, int estimatedTokens, Guid? relatedEntityId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users
            .Query()
            .Include(u => u.TierMembership)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var maxQuota = user.TierMembership?.AiTokens ?? 0;
        if (user.CurrentAiTokenUsage + estimatedTokens > maxQuota)
            throw new QuotaExceededException($"Token quota exceeded. Available: {maxQuota - user.CurrentAiTokenUsage}, requested: {estimatedTokens}.");

        var ledger = new TokenLedger
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RelatedEntityId = relatedEntityId,
            OperationType = operationType,
            Status = TokenLedgerStatus.Reserved,
            EstimatedTokens = estimatedTokens,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.TokenLedgers.AddAsync(ledger, ct);
        user.CurrentAiTokenUsage += estimatedTokens;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return new TokenReservationDto(ledger.Id, userId, operationType, estimatedTokens, ledger.CreatedAt);
    }

    public async Task SettleAsync(Guid ledgerId, int actualTokens, CancellationToken ct = default)
    {
        var ledger = await _unitOfWork.TokenLedgers
            .Query()
            .FirstOrDefaultAsync(l => l.Id == ledgerId, ct)
            ?? throw new KeyNotFoundException("Ledger entry not found.");

        if (ledger.Status != TokenLedgerStatus.Reserved)
            return; // Already settled or refunded

        var user = await _unitOfWork.Users.GetByIdAsync(ledger.UserId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var refund = Math.Max(0, ledger.EstimatedTokens - actualTokens);
        user.CurrentAiTokenUsage -= refund;
        _unitOfWork.Users.Update(user);

        ledger.Status = TokenLedgerStatus.Committed;
        ledger.ActualTokens = actualTokens;
        _unitOfWork.TokenLedgers.Update(ledger);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RefundAsync(Guid ledgerId, string reason, CancellationToken ct = default)
    {
        var ledger = await _unitOfWork.TokenLedgers
            .Query()
            .FirstOrDefaultAsync(l => l.Id == ledgerId, ct)
            ?? throw new KeyNotFoundException("Ledger entry not found.");

        if (ledger.Status != TokenLedgerStatus.Reserved)
            return;

        var user = await _unitOfWork.Users.GetByIdAsync(ledger.UserId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        user.CurrentAiTokenUsage -= ledger.EstimatedTokens;
        if (user.CurrentAiTokenUsage < 0) user.CurrentAiTokenUsage = 0;
        _unitOfWork.Users.Update(user);

        ledger.Status = TokenLedgerStatus.Refunded;
        ledger.FailureReason = reason;
        _unitOfWork.TokenLedgers.Update(ledger);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<TokenWalletResponseDto> GetBalanceAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users
            .Query()
            .Include(u => u.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var maxQuota = user.TierMembership?.AiTokens ?? 0;
        var balance = new TokenBalanceDto(
            user.CurrentAiTokenUsage,
            maxQuota,
            maxQuota - user.CurrentAiTokenUsage,
            maxQuota);

        var recent = await _unitOfWork.TokenLedgers
            .Query()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(20)
            .AsNoTracking()
            .Select(l => new
            {
                l.Id,
                l.OperationType,
                Status = l.Status,
                l.EstimatedTokens,
                l.ActualTokens,
                l.FailureReason,
                l.CreatedAt
            })
            .ToListAsync(ct);

        var history = recent.Select(l => new TokenWalletHistoryDto(
            l.Id, l.OperationType, l.Status.ToString(), l.EstimatedTokens, l.ActualTokens, l.FailureReason, l.CreatedAt)).ToList();

        return new TokenWalletResponseDto(balance, history);
    }
}
