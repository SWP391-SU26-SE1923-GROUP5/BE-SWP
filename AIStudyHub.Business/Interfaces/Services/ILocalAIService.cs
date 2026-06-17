using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ILocalAIService
{
    Task<string> SendMessageAsync(string message);
<<<<<<< HEAD
    Task<string> SendMessageAsync(string message, float temperature);
=======
    Task<string> SendMessageAsync(string message, float? temperature, int? numPredict = null, CancellationToken cancellationToken = default);
    Task<string> SendChatAsync(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        CancellationToken cancellationToken = default);
>>>>>>> b2820b1166319b4413a27b83e4366c51cf8c1b80
    Task<ReadOnlyMemory<float>> CreateEmbeddingFromText(string message);
}

public sealed record ChatTurn(string Role, string Content);
