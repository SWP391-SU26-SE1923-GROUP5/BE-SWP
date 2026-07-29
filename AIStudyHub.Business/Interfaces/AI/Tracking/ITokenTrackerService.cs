namespace AIStudyHub.Business.Interfaces.AI.Tracking;

public interface ITokenTrackerService
{
    Task<bool> HasQuotaAsync(Guid userId, int estimatedTokens, CancellationToken ct = default);
    Task RecordUsageAsync(Guid userId, int inputTokens, int outputTokens, string operationType, CancellationToken ct = default);
    Task RecordGenerationUsageAsync(
        Guid operationId,
        Guid userId,
        Guid relatedEntityId,
        int inputTokens,
        int outputTokens,
        string operationType);
    Task<int> GetRemainingQuotaAsync(Guid userId, CancellationToken ct = default);
    Task<(int currentUsage, int limit)> GetUsageInfoAsync(Guid userId, CancellationToken ct = default);
}
