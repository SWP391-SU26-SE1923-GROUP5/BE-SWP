namespace AIStudyHub.Business.Configuration;

public class KernelMemoryOptions
{
    public QdrantOptions Qdrant { get; set; } = new();

    public ChunkingOptions Chunking { get; set; } = new();
}

public class QdrantOptions
{
    public string Host { get; set; } = "http://localhost:6333";
    public int VectorSize { get; set; } = 1536;
    public string CollectionName { get; set; } = "aistudyhub";
}



public class ChunkingOptions
{
    public int MaxTokensPerChunk { get; set; } = 1024;
    public int OverlapTokens { get; set; } = 128;
    public int MinTokensPerChunk { get; set; } = 128;
}
