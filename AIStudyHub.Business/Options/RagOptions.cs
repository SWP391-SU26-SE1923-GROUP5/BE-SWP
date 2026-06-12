namespace AIStudyHub.Business.Options;

public sealed class RagOptions
{
    // GPT4All Local LLM Settings
    public string Gpt4AllUrl { get; set; } = "http://localhost:6768";
    public string Gpt4AllModel { get; set; } = "llama-3.2-1b-instruct.Q4_0.gguf";
    public bool UseLocalLlm { get; set; } = true;
    public int MaxTokens { get; set; } = 1000;
    public float Temperature { get; set; } = 0.3f;

    // Embedding Settings
    public bool UseLocalEmbeddings { get; set; } = true;
    public string LocalEmbeddingModel { get; set; } = "all-MiniLM-L6-v2";
    public string LocalEmbeddingUrl { get; set; } = "http://localhost:5000";

    // Fallback: OpenAI for embeddings (if local not available)
    public string? OpenAiApiKey { get; set; }
    public string OpenAiEmbeddingModel { get; set; } = "text-embedding-3-small";

    // Pinecone (optional - can skip if using local vector DB)
    public string? PineconeApiKey { get; set; }
    public string? PineconeEnvironment { get; set; }
    public string PineconeIndexName { get; set; } = "aistudyhub-docs";

    // Chunking Settings
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 50;
    public int TopKChunks { get; set; } = 5;
}
