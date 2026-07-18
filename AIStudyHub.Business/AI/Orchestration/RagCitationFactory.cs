using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.AI.Orchestration;

public sealed record RagCitationSource(SearchResult Result, CitationInfo Citation);

public sealed class RagCitationFactory
{
    private readonly ILogger<RagCitationFactory> _logger;

    public RagCitationFactory(ILogger<RagCitationFactory> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<RagCitationSource> Create(
        IReadOnlyList<SearchResult> results,
        IReadOnlyCollection<Guid>? allowedDocumentIds)
    {
        HashSet<Guid>? allowed = allowedDocumentIds is null
            ? null
            : allowedDocumentIds.Where(id => id != Guid.Empty).ToHashSet();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sources = new List<RagCitationSource>();

        foreach (var result in results)
        {
            var rawDocumentId = result.Metadata.GetValueOrDefault("documentId");
            if (!Guid.TryParse(rawDocumentId, out var documentId) || documentId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Citation source {Source} rejected because documentId metadata {DocumentId} is invalid",
                    result.Source,
                    rawDocumentId);
                continue;
            }

            if (allowed is not null && !allowed.Contains(documentId))
            {
                _logger.LogWarning(
                    "Citation source {Source} rejected because document {DocumentId} is outside the allowed set",
                    result.Source,
                    documentId);
                continue;
            }

            if (string.IsNullOrWhiteSpace(result.Source) || string.IsNullOrWhiteSpace(result.Content))
            {
                _logger.LogWarning(
                    "Citation source {Source} rejected because required source metadata is blank",
                    result.Source);
                continue;
            }

            var pageNumber = ParseInt(result.Metadata.GetValueOrDefault("pageNumber"));
            var chunkIndex = ParseInt(result.Metadata.GetValueOrDefault("chunkIndex"));
            var key = chunkIndex.HasValue
                ? $"{documentId:D}|chunk:{chunkIndex.Value}"
                : $"{documentId:D}|page:{pageNumber?.ToString() ?? "none"}|content:{result.Content}";
            if (!seen.Add(key))
            {
                continue;
            }

            var highlight = CitationHighlightability.FromMetadata(result.Metadata);
            var citation = new CitationInfo(
                documentId,
                result.Source,
                result.Content,
                result.Score,
                pageNumber,
                result.MatchType,
                highlight.IsHighlightable,
                highlight.Reason,
                sources.Count + 1);
            sources.Add(new RagCitationSource(result, citation));
        }

        return sources;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;
}
