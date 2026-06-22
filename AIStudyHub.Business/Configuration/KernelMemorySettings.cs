namespace AIStudyHub.Business.Configuration;

public class KernelMemorySettings
{
    public QdrantSettings Qdrant { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
    public ChunkingSettings Chunking { get; set; } = new();
}

public class QdrantSettings
{
    public string Host { get; set; } = "http://localhost:6333";
    public int VectorSize { get; set; } = 1536;
    public string CollectionName { get; set; } = "aistudyhub";
}

public class OllamaSettings
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string GenerationModel { get; set; } = "llama3.1";
}

public class ChunkingSettings
{
    public int MaxTokensPerChunk { get; set; } = 1024;
    public int OverlapTokens { get; set; } = 128;
    public int MinTokensPerChunk { get; set; } = 128;
}
