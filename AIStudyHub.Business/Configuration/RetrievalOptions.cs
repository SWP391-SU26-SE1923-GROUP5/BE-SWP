namespace AIStudyHub.Business.Configuration;

public class RetrievalOptions
{
    public int TopK { get; set; } = 10;
    public int RerankTopK { get; set; } = 5;
    public bool UseHybridSearch { get; set; } = true;
    public bool UseReranking { get; set; } = true;
    public double RerankThreshold { get; set; } = 0.3;
}
