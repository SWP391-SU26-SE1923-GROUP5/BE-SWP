namespace AIStudyHub.Business.Interfaces.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts);
    int GetEmbeddingDimension();
}
