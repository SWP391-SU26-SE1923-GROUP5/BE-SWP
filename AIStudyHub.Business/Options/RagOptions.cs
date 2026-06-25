namespace AIStudyHub.Business.Options;

public class RagOptions
{
    public string QdrantHost { get; set; } = "http://localhost:6333";
    public int VectorDimension { get; set; } = 1536;
    public string CollectionName { get; set; } = "documents";

    public int TopKChunks { get; set; } = 10;
    public int ChunkSize { get; set; } = 1000;
    public int ChunkOverlap { get; set; } = 200;
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
    public string OpenAIApiKey { get; set; } = "";
    public string OpenAIChatModel { get; set; } = "gpt-5-mini";
    public string OpenAIEmbeddingModel { get; set; } = "text-embedding-3-small";
}
