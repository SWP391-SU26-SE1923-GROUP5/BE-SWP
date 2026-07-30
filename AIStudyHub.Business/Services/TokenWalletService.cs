using AIStudyHub.Business.DTOs.TokenWallet;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed class TokenWalletService : ITokenWalletService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public TokenWalletService(
        IUnitOfWork unitOfWork,
        IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _unitOfWork = unitOfWork;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<TokenReservationDto> ReserveAsync(Guid userId, string operationType, int estimatedTokens, Guid? relatedEntityId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var reservedAt = DateTime.UtcNow;
        var ledger = new TokenLedger
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RelatedEntityId = relatedEntityId,
            OperationType = operationType,
            Status = TokenLedgerStatus.Reserved,
            EstimatedTokens = estimatedTokens,
            CreatedAt = reservedAt
        };

        var updatedUsers = await context.Users
            .Where(user =>
                user.Id == userId
                && (user.TierMembership!.AiTokens == 0
                    || user.CurrentAiTokenUsage
                        <= user.TierMembership.AiTokens - estimatedTokens))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        user => user.CurrentAiTokenUsage,
                        user => user.CurrentAiTokenUsage + estimatedTokens)
                    .SetProperty(user => user.UpdatedAt, reservedAt),
                ct);

        if (updatedUsers != 1)
        {
            var balance = await context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.CurrentAiTokenUsage,
                    MaxQuota = user.TierMembership == null
                        ? 0
                        : user.TierMembership.AiTokens
                })
                .SingleOrDefaultAsync(ct)
                ?? throw new UnauthorizedAccessException("User not found.");

            throw new QuotaExceededException(
                $"Token quota exceeded. Available: {balance.MaxQuota - balance.CurrentAiTokenUsage}, requested: {estimatedTokens}.");
        }

        context.TokenLedgers.Add(ledger);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new TokenReservationDto(ledger.Id, userId, operationType, estimatedTokens, reservedAt);
    }

    public async Task SettleAsync(Guid ledgerId, int actualTokens, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var ledger = await context.TokenLedgers
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == ledgerId, ct)
            ?? throw new KeyNotFoundException("Ledger entry not found.");

        if (ledger.Status != TokenLedgerStatus.Reserved)
            return; // Already settled or refunded

        var settledAt = DateTime.UtcNow;
        var refund = Math.Max(0, ledger.EstimatedTokens - actualTokens);
        var transitionedLedgers = await context.TokenLedgers
            .Where(entry =>
                entry.Id == ledgerId
                && entry.Status == TokenLedgerStatus.Reserved)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.Status, TokenLedgerStatus.Committed)
                    .SetProperty(entry => entry.ActualTokens, actualTokens)
                    .SetProperty(entry => entry.UpdatedAt, settledAt),
                ct);

        if (transitionedLedgers == 0)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        var updatedUsers = await context.Users
            .Where(user => user.Id == ledger.UserId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        user => user.CurrentAiTokenUsage,
                        user => user.CurrentAiTokenUsage - refund)
                    .SetProperty(user => user.UpdatedAt, settledAt),
                ct);
        if (updatedUsers != 1)
            throw new UnauthorizedAccessException("User not found.");

        await transaction.CommitAsync(ct);
    }

    public async Task RefundAsync(Guid ledgerId, string reason, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var ledger = await context.TokenLedgers
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == ledgerId, ct)
            ?? throw new KeyNotFoundException("Ledger entry not found.");

        if (ledger.Status != TokenLedgerStatus.Reserved)
            return;

        var refundedAt = DateTime.UtcNow;
        var transitionedLedgers = await context.TokenLedgers
            .Where(entry =>
                entry.Id == ledgerId
                && entry.Status == TokenLedgerStatus.Reserved)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.Status, TokenLedgerStatus.Refunded)
                    .SetProperty(entry => entry.FailureReason, reason)
                    .SetProperty(entry => entry.UpdatedAt, refundedAt),
                ct);

        if (transitionedLedgers == 0)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        var updatedUsers = await context.Users
            .Where(user => user.Id == ledger.UserId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        user => user.CurrentAiTokenUsage,
                        user => user.CurrentAiTokenUsage >= ledger.EstimatedTokens
                            ? user.CurrentAiTokenUsage - ledger.EstimatedTokens
                            : 0)
                    .SetProperty(user => user.UpdatedAt, refundedAt),
                ct);
        if (updatedUsers != 1)
            throw new UnauthorizedAccessException("User not found.");

        await transaction.CommitAsync(ct);
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
