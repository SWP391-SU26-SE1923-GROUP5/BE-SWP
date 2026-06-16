using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AIStudyHub.Business.Services
{
    public class LocalAIService : ILocalAIService
    {
        private readonly RagOptions _options;
        private readonly HttpClient _httpClient;

        public LocalAIService(IOptions<RagOptions> options, IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _httpClient = httpClientFactory.CreateClient();
            if (_httpClient.Timeout == Timeout.InfiniteTimeSpan || _httpClient.Timeout == default)
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(120);
            }
        }

        public Task<string> SendMessageAsync(string message)
        {
            return SendMessageAsync(message, temperature: null, numPredict: null, CancellationToken.None);
        }

        public async Task<string> SendMessageAsync(
            string message,
            float? temperature,
            int? numPredict = null,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                model = _options.OllamaModel,
                stream = false,
                format = "json",
                options = new
                {
                    temperature = temperature ?? _options.Temperature,
                    num_predict = numPredict ?? _options.MaxTokens
                },
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = message
                    }
                }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.OllamaUrl}/api/chat")
            {
                Content = JsonContent.Create(payload)
            };

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            return json.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

        public async Task<string> SendChatAsync(
            string systemPrompt,
            IReadOnlyList<ChatTurn> history,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.OllamaUrl))
                throw new InvalidOperationException("OllamaUrl is not configured.");

            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            if (history is { Count: > 0 })
            {
                foreach (var turn in history)
                {
                    if (string.IsNullOrWhiteSpace(turn.Content))
                        continue;
                    messages.Add(new { role = turn.Role, content = turn.Content });
                }
            }

            messages.Add(new { role = "user", content = userMessage });

            var payload = new
            {
                model = _options.OllamaModel,
                stream = false,
                options = new
                {
                    temperature = _options.Temperature,
                    num_predict = _options.MaxTokens
                },
                messages = messages.ToArray()
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.OllamaUrl}/api/chat")
            {
                Content = JsonContent.Create(payload)
            };

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            return json.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

        public async Task<ReadOnlyMemory<float>> CreateEmbeddingFromText(string message)
        {
            var payload = new
            {
                model = _options.OllamaEmbeddingModel,
                input = message
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_options.OllamaUrl}/api/embed",
                payload);

            response.EnsureSuccessStatusCode();

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());

            var embeddingArray = json.RootElement
                .GetProperty("embeddings")[0];

            float[] embedding = embeddingArray
                .EnumerateArray()
                .Select(x => x.GetSingle())
                .ToArray();

            return embedding;
        }
    }
}
