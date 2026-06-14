using System.Threading;
using System.Threading.Tasks;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IOpenAIService
{
    Task<string> SendMessageAsync(string message);
    Task<ReadOnlyMemory<float>> CreateEmbeddingFromText(string message);
}
