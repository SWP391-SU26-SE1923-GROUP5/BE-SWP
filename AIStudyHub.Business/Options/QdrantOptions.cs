namespace AIStudyHub.Business.Options;

public sealed class QdrantOptions
{
    public string Url { get; set; } = "http://localhost:6333";
    public int GrpcPort { get; set; } = 6334;
    public bool UseSsl { get; set; } = false;
    public string CollectionName { get; set; } = "aistudyhub-docs";
    public int VectorSize { get; set; } = 768;
}
