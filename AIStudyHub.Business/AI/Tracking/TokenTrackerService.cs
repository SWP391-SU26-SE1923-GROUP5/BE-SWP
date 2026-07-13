using AIStudyHub.Business.Interfaces.AI.Tracking;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.AI.Tracking;

public class TokenTrackerService : ITokenTrackerService
{
    private readonly IUnitOfWork _unitOfWork;

    public TokenTrackerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
        var user = await _unitOfWork.Users
            .Query()
            .Include(u => u.TierMembership)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return;

        user.CurrentAiTokenUsage += inputTokens + outputTokens;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);
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
}
