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
                // ----- OLLAMA (COMMENTED OUT) -----
                /*
                var payload = new
                {
                    model = _options.OllamaModel,
                    stream = false,
                    temperature = temperature,
                    messages = new[]
                    {
                        new { role = "user", content = message }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync($"{_options.OllamaUrl}/api/chat", payload);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Ollama chat failed: {Status}. Body: {Body}", response.StatusCode, errorBody);
                    return string.Empty;
                }

                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                return json.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
                */

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
            // ----- OLLAMA (COMMENTED OUT) -----
            /*
            var payload = new { model = _options.OllamaEmbeddingModel, input = message };
            var response = await _httpClient.PostAsJsonAsync($"{_options.OllamaUrl}/api/embed", payload);
            response.EnsureSuccessStatusCode();

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var embeddingArray = json.RootElement.GetProperty("embeddings")[0];

            float[] embedding = embeddingArray.EnumerateArray().Select(x => x.GetSingle()).ToArray();
            return embedding;
            */

            // ----- OPENAI -----
            var result = await _embeddingClient.GenerateEmbeddingAsync(message);
            return result.Value.ToFloats();
        }

        public async Task<List<float[]>> CreateEmbeddingsFromTexts(List<string> messages)
        {
            var result = new List<float[]>();

            // ----- OLLAMA (COMMENTED OUT) -----
            /*
            int batchSize = 20; 
            for (int i = 0; i < messages.Count; i += batchSize)
            {
                var batch = messages.Skip(i).Take(batchSize).ToList();
                var payload = new { model = _options.OllamaEmbeddingModel, input = batch };

                var response = await _httpClient.PostAsJsonAsync($"{_options.OllamaUrl}/api/embed", payload);
                response.EnsureSuccessStatusCode();

                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                var embeddingsArray = json.RootElement.GetProperty("embeddings");

                foreach (var embeddingElement in embeddingsArray.EnumerateArray())
                {
                    result.Add(embeddingElement.EnumerateArray().Select(x => x.GetSingle()).ToArray());
                }
            }
            */

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
