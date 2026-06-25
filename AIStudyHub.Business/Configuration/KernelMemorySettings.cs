namespace AIStudyHub.Business.Configuration;

public class KernelMemorySettings
{
    public QdrantSettings Qdrant { get; set; } = new();

    public OpenAISettings OpenAI { get; set; } = new();
    public ChunkingSettings Chunking { get; set; } = new();
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string TextModel { get; set; } = "gpt-4o-mini";
}

public class QdrantSettings
{
    public string Host { get; set; } = "http://localhost:6333";
    public int VectorSize { get; set; } = 1536;
    public string CollectionName { get; set; } = "aistudyhub";
}



public class ChunkingSettings
{
    public int MaxTokensPerChunk { get; set; } = 1024;
    public int OverlapTokens { get; set; } = 128;
    public int MinTokensPerChunk { get; set; } = 128;
}
