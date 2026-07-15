using System.Data;
using AIStudyHub.Business.AI;
using AIStudyHub.Data;
using AIStudyHub.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed record DocumentReindexClaim(
    Guid DocumentId, Guid UserId, string FileLink, string FileName,
    string ContentType, Guid ClaimId, Guid IndexRunId);

public interface IDocumentReindexClaimService
{
    Task<IReadOnlyList<DocumentReindexClaim>> ClaimBatchAsync(
        int batchSize, TimeSpan claimTimeout, int maxAttempts, CancellationToken ct);
    Task FailClaimAsync(Guid documentId, Guid claimId, string error, CancellationToken ct);
}

public sealed class DocumentReindexClaimService : IDocumentReindexClaimService
{
    private readonly ApplicationDbContext _db;

    public DocumentReindexClaimService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DocumentReindexClaim>> ClaimBatchAsync(
        int batchSize, TimeSpan claimTimeout, int maxAttempts, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now - claimTimeout;
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var documents = await _db.Documents
            .Where(d => d.Status == DocumentStatus.Done
                && d.ProcessingVersion < DocumentIngestionVersion.Current
                && d.LifecycleStatus == DocumentLifecycleStatus.Active
                && d.FileLink != null
                && d.FileName != null
                && (d.FileName.EndsWith(".pdf")
                    || d.FileName.EndsWith(".docx")
                    || d.FileName.EndsWith(".txt")
                    || d.FileName.EndsWith(".md")
                    || d.FileName.EndsWith(".jpg")
                    || d.FileName.EndsWith(".jpeg")
                    || d.FileName.EndsWith(".png")
                    || d.FileName.EndsWith(".webp")
                    || d.FileName.EndsWith(".gif"))
                && d.ReindexAttemptCount < maxAttempts
                && (d.ReindexClaimId == null || d.ReindexClaimedAt == null || d.ReindexClaimedAt < staleBefore))
            .OrderBy(d => d.UpdatedAt ?? d.CreatedAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(ct);

        var claims = new List<DocumentReindexClaim>(documents.Count);
        foreach (var document in documents)
        {
            var claimId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            document.ReindexClaimId = claimId;
            document.ReindexClaimedAt = now;
            document.ReindexAttemptCount++;
            claims.Add(new DocumentReindexClaim(
                document.Id, document.UserId, document.FileLink!, document.FileName!,
                document.FileType ?? "application/octet-stream", claimId, runId));
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return claims;
    }

    public async Task FailClaimAsync(Guid documentId, Guid claimId, string error, CancellationToken ct)
    {
        var document = await _db.Documents.SingleOrDefaultAsync(
            d => d.Id == documentId && d.ReindexClaimId == claimId, ct);
        if (document == null) return;
        document.ReindexClaimId = null;
        document.ReindexClaimedAt = null;
        document.LastReindexError = error.Length > 2000 ? error[..2000] : error;
        await _db.SaveChangesAsync(ct);
    }
}
