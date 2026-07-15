namespace AIStudyHub.Business.AI.Orchestration;

public static class CitationHighlightability
{
    public static (bool IsHighlightable, string? Reason) FromMetadata(
        IReadOnlyDictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue("contentType", out var contentType)
            || !metadata.TryGetValue("isHighlightable", out var rawHighlightable)
            || !bool.TryParse(rawHighlightable, out var markedHighlightable))
            return (false, "legacy_unclassified");

        return contentType.ToLowerInvariant() switch
        {
            "verbatim" when markedHighlightable => (true, null),
            "summary" => (false, "synthetic_summary"),
            "alttext" => (false, "document_alt_text"),
            "ocr" => (false, "ocr_text"),
            _ => (false, "legacy_unclassified")
        };
    }
}
