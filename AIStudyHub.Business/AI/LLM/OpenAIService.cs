using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace AIStudyHub.Business.AI.LLM
{
    public class OpenAIService: IOpenAIService
    {
        private readonly RagOptions _options;
        private readonly ILogger<OpenAIService> _logger;
        
        private readonly ChatClient _chatClient;
        private readonly EmbeddingClient _embeddingClient;

        public OpenAIService(IOptions<RagOptions> options, ILogger<OpenAIService> logger)
        {
            _options = options.Value;
            _logger = logger;

            _chatClient = new ChatClient(_options.OpenAIChatModel, _options.OpenAIApiKey);
            _embeddingClient = new EmbeddingClient(_options.OpenAIEmbeddingModel, _options.OpenAIApiKey);
        }
        public Task<string> SendMessageAsync(string message)
            => SendMessageAsync(message, 0.2f);

        public async Task<string> SendMessageAsync(string message, float temperature)
        {
            try
            {
                var options = new ChatCompletionOptions();
                
                if (!_options.OpenAIChatModel.Contains("o1") && !_options.OpenAIChatModel.Contains("gpt-5"))
                {
                    options.Temperature = temperature;
                }

                var completion = await _chatClient.CompleteChatAsync(
                    new[] { new UserChatMessage(message) },
                    options);

                return completion.Value.Content[0].Text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAIService.SendMessageAsync failed (OpenAI)");
                return string.Empty;
            }
        }
        public async Task<ReadOnlyMemory<float>> CreateEmbeddingFromText(string message)
        {
            var result = await _embeddingClient.GenerateEmbeddingAsync(message);
            return result.Value.ToFloats();
        }

        public async Task<List<float[]>> CreateEmbeddingsFromTexts(List<string> messages)
        {
            var result = new List<float[]>();

            int batchSize = 100;
            for (int i = 0; i < messages.Count; i += batchSize)
            {
                var batch = messages.Skip(i).Take(batchSize).ToList();
                var response = await _embeddingClient.GenerateEmbeddingsAsync(batch);
                
                foreach (var embedding in response.Value)
                {
                    result.Add(embedding.ToFloats().ToArray());
                }
            }

            return result;
        }
    }
}
