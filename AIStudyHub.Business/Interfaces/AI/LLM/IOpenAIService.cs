using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIStudyHub.Business.Interfaces.AI.LLM;

public interface IOpenAIService
{
    Task<string> SendMessageAsync(string message);
    Task<string> SendMessageAsync(string message, float temperature);
    Task<ReadOnlyMemory<float>> CreateEmbeddingFromText(string message);
    Task<List<float[]>> CreateEmbeddingsFromTexts(List<string> messages);
}
