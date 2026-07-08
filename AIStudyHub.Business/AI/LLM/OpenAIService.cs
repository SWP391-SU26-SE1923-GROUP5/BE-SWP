using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.DTOs.AI;
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

        public Task<TokenUsageResult> SendMessageWithUsageAsync(string message)
            => SendMessageWithUsageAsync(message, 0.2f);

        public async Task<TokenUsageResult> SendMessageWithUsageAsync(string message, float temperature)
        {
            try
            {
                var options = new ChatCompletionOptions();
                
                if (!_options.OpenAIChatModel.Contains("o1") && !_options.OpenAIChatModel.Contains("gpt-5"))
                {
                    options.Temperature = temperature;
                }

                var result = await _chatClient.CompleteChatAsync(
                    new[] { new UserChatMessage(message) },
                    options);

                var completion = result.Value;
                var usage = completion.Usage;
                var inputTokens = (int)(usage?.InputTokenCount ?? 0);
                var outputTokens = (int)(usage?.OutputTokenCount ?? 0);
                var text = completion.Content[0].Text;

                return new TokenUsageResult(text, inputTokens, outputTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAIService.SendMessageWithUsageAsync failed (OpenAI)");
                return new TokenUsageResult(string.Empty, 0, 0);
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

            int batchSize = 500;
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
