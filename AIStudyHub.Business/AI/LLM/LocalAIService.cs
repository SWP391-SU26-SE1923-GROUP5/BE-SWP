using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace AIStudyHub.Business.AI.LLM
{
    public class LocalAIService: ILocalAIService
    {
        private readonly RagOptions _options;
        // private readonly HttpClient _httpClient;
        private readonly ILogger<LocalAIService> _logger;
        
        // OpenAI Clients
        private readonly ChatClient _chatClient;
        private readonly EmbeddingClient _embeddingClient;

        public LocalAIService(IOptions<RagOptions> options, ILogger<LocalAIService> logger)
        {
            _options = options.Value;
            // _httpClient = httpClientFactory.CreateClient("LlmClient");
            // _httpClient.Timeout = TimeSpan.FromMinutes(10);
            _logger = logger;

            // Initialize OpenAI clients
            _chatClient = new ChatClient(_options.OpenAIChatModel, _options.OpenAIApiKey);
            _embeddingClient = new EmbeddingClient(_options.OpenAIEmbeddingModel, _options.OpenAIApiKey);
        }
        public Task<string> SendMessageAsync(string message)
            => SendMessageAsync(message, 0.2f);

        public async Task<string> SendMessageAsync(string message, float temperature)
        {
            try
            {


                // ----- OPENAI -----
                var options = new ChatCompletionOptions();
                
                // Some models (like o1-mini or custom endpoints aliased as gpt-5-mini) 
                // strictly reject custom temperatures and require the default (1).
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
                _logger.LogError(ex, "LocalAIService.SendMessageAsync failed (OpenAI)");
                return string.Empty;
            }
        }
        public async Task<ReadOnlyMemory<float>> CreateEmbeddingFromText(string message)
        {


            // ----- OPENAI -----
            var result = await _embeddingClient.GenerateEmbeddingAsync(message);
            return result.Value.ToFloats();
        }

        public async Task<List<float[]>> CreateEmbeddingsFromTexts(List<string> messages)
        {
            var result = new List<float[]>();



            // ----- OPENAI -----
            // OpenAI handles reasonably large batches up to thousands of texts natively.
            // Using a conservative batch size of 100 just to be safe.
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
