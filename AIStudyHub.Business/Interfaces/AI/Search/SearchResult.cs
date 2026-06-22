namespace AIStudyHub.Business.Interfaces.AI.Search;

public record SearchResult(
    string Content,
    double Score,
    string Source,
    Dictionary<string, string> Metadata
);
