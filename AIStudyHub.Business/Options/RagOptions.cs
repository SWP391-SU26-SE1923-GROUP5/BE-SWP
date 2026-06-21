namespace AIStudyHub.Business.Options;

public class RagOptions
{
    public string QdrantHost { get; set; } = "http://localhost:6333";
    public int VectorDimension { get; set; } = 1536;
    public string CollectionName { get; set; } = "documents";
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string LlmModel { get; set; } = "llama3.1";
}
