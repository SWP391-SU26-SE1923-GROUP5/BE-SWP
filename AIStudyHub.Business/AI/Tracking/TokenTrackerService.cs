using AIStudyHub.Business.Interfaces.AI.Tracking;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.AI.Tracking;

public class TokenTrackerService : ITokenTrackerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public TokenTrackerService(
        IUnitOfWork unitOfWork,
        IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _unitOfWork = unitOfWork;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> HasQuotaAsync(Guid userId, int estimatedTokens, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users
            .Query()
            .Include(u => u.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return false;

        var limit = user.TierMembership?.AiTokens ?? 0;

        // If no tier or unlimited (0 means unlimited for now), allow
        if (limit == 0)
            return true;

        return user.CurrentAiTokenUsage + estimatedTokens <= limit;
    }

    public async Task RecordUsageAsync(Guid userId, int inputTokens, int outputTokens, string operationType, CancellationToken ct = default)
    {
        var totalTokens = checked(inputTokens + outputTokens);
        await _unitOfWork.Users
            .Query()
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        user => user.CurrentAiTokenUsage,
                        user => user.CurrentAiTokenUsage + totalTokens)
                    .SetProperty(
                        user => user.UpdatedAt,
                        DateTime.UtcNow),
                ct);
    }

    public async Task RecordGenerationUsageAsync(
        Guid operationId,
        Guid userId,
        Guid relatedEntityId,
        int inputTokens,
        int outputTokens,
        string operationType)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException(
                "A token accounting operation id is required.",
                nameof(operationId));
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not found.");
        if (inputTokens < 0)
            throw new ArgumentOutOfRangeException(nameof(inputTokens));
        if (outputTokens < 0)
            throw new ArgumentOutOfRangeException(nameof(outputTokens));
        if (string.IsNullOrWhiteSpace(operationType))
            throw new ArgumentException(
                "A token accounting operation type is required.",
                nameof(operationType));

        var totalTokens = checked(inputTokens + outputTokens);

        await using var context =
            await _dbContextFactory.CreateDbContextAsync(
                CancellationToken.None);
        await using var transaction =
            await context.Database.BeginTransactionAsync(
                CancellationToken.None);

        var existing = await context.TokenLedgers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                ledger => ledger.Id == operationId,
                CancellationToken.None);
        if (existing is not null)
        {
            EnsureMatchingGenerationLedger(
                existing,
                userId,
                relatedEntityId,
                totalTokens,
                operationType);
            await transaction.CommitAsync(CancellationToken.None);
            return;
        }

        context.TokenLedgers.Add(new TokenLedger
        {
            Id = operationId,
            UserId = userId,
            RelatedEntityId = relatedEntityId,
            OperationType = operationType,
            Status = TokenLedgerStatus.Committed,
            EstimatedTokens = totalTokens,
            ActualTokens = totalTokens
        });

        try
        {
            await context.SaveChangesAsync(CancellationToken.None);

            var updatedAt = DateTime.UtcNow;
            var updatedUsers = await context.Users
                .Where(user => user.Id == userId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            user => user.CurrentAiTokenUsage,
                            user => user.CurrentAiTokenUsage + totalTokens)
                        .SetProperty(
                            user => user.UpdatedAt,
                            updatedAt),
                    CancellationToken.None);
            if (updatedUsers != 1)
                throw new UnauthorizedAccessException("User not found.");

            await transaction.CommitAsync(CancellationToken.None);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (await VerifyMatchingGenerationLedgerAsync(
                    operationId,
                    userId,
                    relatedEntityId,
                    totalTokens,
                    operationType))
            {
                return;
            }

            throw;
        }
    }

    public async Task<int> GetRemainingQuotaAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users
            .Query()
            .Include(u => u.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return 0;

        var limit = user.TierMembership?.AiTokens ?? 0;
        return Math.Max(0, limit - user.CurrentAiTokenUsage);
    }

    public async Task<(int currentUsage, int limit)> GetUsageInfoAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users
            .Query()
            .Include(u => u.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return (0, 0);

        var limit = user.TierMembership?.AiTokens ?? 0;
        return (user.CurrentAiTokenUsage, limit);
    }

    private async Task<bool> VerifyMatchingGenerationLedgerAsync(
        Guid operationId,
        Guid userId,
        Guid relatedEntityId,
        int totalTokens,
        string operationType)
    {
        await using var verificationContext =
            await _dbContextFactory.CreateDbContextAsync(
                CancellationToken.None);
        var existing = await verificationContext.TokenLedgers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                ledger => ledger.Id == operationId,
                CancellationToken.None);
        if (existing is null)
            return false;

        EnsureMatchingGenerationLedger(
            existing,
            userId,
            relatedEntityId,
            totalTokens,
            operationType);
        return true;
    }

    private static void EnsureMatchingGenerationLedger(
        TokenLedger ledger,
        Guid userId,
        Guid relatedEntityId,
        int totalTokens,
        string operationType)
    {
        if (ledger.UserId != userId
            || ledger.RelatedEntityId != relatedEntityId
            || !string.Equals(
                ledger.OperationType,
                operationType,
                StringComparison.Ordinal)
            || ledger.Status != TokenLedgerStatus.Committed
            || ledger.EstimatedTokens != totalTokens
            || ledger.ActualTokens != totalTokens)
        {
            throw new InvalidOperationException(
                "Token accounting operation id is already associated with a different operation.");
        }
    }
}
