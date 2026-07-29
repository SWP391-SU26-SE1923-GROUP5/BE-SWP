namespace AIStudyHub.Business.Options;

public class RagOptions
{
    public string QdrantHost { get; set; } = "http://localhost:6333";
    public int VectorDimension { get; set; } = 1536;
    public string CollectionName { get; set; } = "documents";

    public int TopKChunks { get; set; } = 10;
    /// <summary>Target chunk size in characters (approximate, sentence-aligned). 1 token ≈ 4 chars.</summary>
    public int ChunkSize { get; set; } = 2048;
    /// <summary>Overlap between chunks in characters (approximate, sentence-aligned).</summary>
    public int ChunkOverlap { get; set; } = 256;
    public string OpenAIApiKey { get; set; } = "";
    public string OpenAIChatModel { get; set; } = "gpt-5-mini";
    public string OpenAIEmbeddingModel { get; set; } = "text-embedding-3-small";
}
