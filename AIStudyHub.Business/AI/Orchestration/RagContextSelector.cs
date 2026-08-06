using System.Globalization;
using AIStudyHub.Business.Interfaces.AI.Search;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.AI.Orchestration;

public sealed record RagContextSource(
    SearchResult Result,
    Guid DocumentId,
    int? PageNumber,
    int? ChunkIndex);

public sealed class RagContextSelector
{
    private readonly ILogger<RagContextSelector> _logger;

    public RagContextSelector(ILogger<RagContextSelector> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<RagContextSource> Select(
        IReadOnlyList<SearchResult> results,
        IReadOnlyCollection<Guid>? allowedDocumentIds)
    {
        HashSet<Guid>? allowed = allowedDocumentIds is null
            ? null
            : allowedDocumentIds.Where(id => id != Guid.Empty).ToHashSet();
        var seenIndexedChunks = new HashSet<(Guid DocumentId, int ChunkIndex)>();
        var seenUnindexedChunks = new HashSet<(Guid DocumentId, int? PageNumber, string Content)>();
        var contexts = new List<RagContextSource>();

        foreach (var result in results)
        {
            var rawDocumentId = result.Metadata.GetValueOrDefault("documentId");
            if (!Guid.TryParse(rawDocumentId, out var documentId) || documentId == Guid.Empty)
            {
                _logger.LogWarning(
                    "RAG context {Source} rejected because documentId metadata is missing or invalid",
                    result.Source);
                continue;
            }

            if (allowed is not null && !allowed.Contains(documentId))
            {
                _logger.LogWarning(
                    "RAG context {Source} rejected because document {DocumentId} is outside the allowed set",
                    result.Source,
                    documentId);
                continue;
            }

            if (string.IsNullOrWhiteSpace(result.Source) || string.IsNullOrWhiteSpace(result.Content))
            {
                _logger.LogWarning(
                    "RAG context for document {DocumentId} rejected because source or content is blank",
                    documentId);
                continue;
            }

            var pageNumber = ParseInt(result.Metadata.GetValueOrDefault("pageNumber"), value => value > 0);
            var chunkIndex = ParseInt(result.Metadata.GetValueOrDefault("chunkIndex"), value => value >= 0);
            var isDuplicate = chunkIndex.HasValue
                ? !seenIndexedChunks.Add((documentId, chunkIndex.Value))
                : !seenUnindexedChunks.Add((documentId, pageNumber, result.Content));
            if (isDuplicate)
                continue;

            contexts.Add(new RagContextSource(result, documentId, pageNumber, chunkIndex));
        }

        return contexts;
    }

    private static int? ParseInt(string? value, Func<int, bool> isValid) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && isValid(parsed)
                ? parsed
                : null;
}
