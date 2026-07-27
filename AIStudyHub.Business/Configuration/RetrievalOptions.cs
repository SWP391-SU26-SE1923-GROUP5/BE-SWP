namespace AIStudyHub.Business.Configuration;

public class RetrievalOptions
{
    public int TopK { get; set; } = 10;
    public int RerankTopK { get; set; } = 5;
    public bool UseHybridSearch { get; set; } = true;
    public bool UseReranking { get; set; } = true;
    public double RerankThreshold { get; set; } = 0.3;
    public int MaxContextChunks { get; set; } = 10;

    /// <summary>Minimum Qdrant relevance score for a chunk to be included as a citation. Chunks below this are excluded.</summary>
    public double CitationMinScore { get; set; } = 0.5;

    /// <summary>Maximum number of citations returned to the frontend per AI response.</summary>
    public int MaxCitations { get; set; } = 5;
}
