using System.Threading;
using System.Threading.Tasks;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ILocalAIService
{
    Task<string> SendMessageAsync(string message);
    Task<string> SendMessageAsync(string message, float temperature);
    Task<ReadOnlyMemory<float>> CreateEmbeddingFromText(string message);
    Task<List<float[]>> CreateEmbeddingsFromTexts(List<string> messages);
}
