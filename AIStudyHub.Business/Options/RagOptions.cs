namespace AIStudyHub.Business.Options;

public sealed class RagOptions
{
    // GPT4All Local LLM Settings

    public bool UseLocalLlm { get; set; } = true;
    public int MaxTokens { get; set; } = 4000;
    public float Temperature { get; set; } = 0.2f;

    // Ollama Local Embedding Settings
    public string? OllamaUrl { get; set; }
    public string? OllamaModel { get; set; }
    public string? OllamaEmbeddingModel { get; set; }

    // Nomic Embedding Settings (cloud API) - Commented out for now
    /*
    public string? NomicApiKey { get; set; }
    public string NomicEmbedModel { get; set; } = "nomic-embed-text-v1";
    public string NomicApiUrl { get; set; } = "https://api-atlas.nomic.ai/v1";
    */

    //OpenAI 

    public string? OpenAIApiKey { get; set; }
    public string OpenAIEmbeddingModel { get; set; } = "";

    public string OpenAIChatModel { get; set; } = "";

    // Pinecone Vector DB Settings
    public string? PineconeApiKey { get; set; }
    public string? PineconeEnvironment { get; set; }
    public string PineconeIndexName { get; set; } = "aistudyhub-docs";

    // Chunking Settings
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 50;
    public int TopKChunks { get; set; } = 5;

    // File Upload Settings
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB default
}
