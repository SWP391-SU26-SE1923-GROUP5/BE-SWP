using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
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

namespace AIStudyHub.Business.Services
{
    public class LocalAIService: ILocalAIService
    {
        private readonly RagOptions _options;
        private readonly HttpClient _httpClient;
        public LocalAIService(IOptions<RagOptions> options, IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _httpClient = httpClientFactory.CreateClient();
        }
        public Task<string> SendMessageAsync(string message)
            => SendMessageAsync(message, 0.2f);

        public async Task<string> SendMessageAsync(string message, float temperature)
        {
            //use
            //_options.OllamaUrl+"/api/chat";
            //model:  _options.OllamaModel 
            /* request:
                        {
                          "model": "gemma4",
                          "messages": [
                            {
                              "role": "user",
                              "content": "why is the sky blue?"
                            }
                          ]
                        }
            response:
                         {
                          "model": "<string>",
                          "created_at": "2023-11-07T05:31:56Z",
                          "message": {
                            "role": "assistant",
                            "content": "<string>",
                            "thinking": "<string>",
                            "tool_calls": [
                              {
                                "function": {
                                  "name": "<string>",
                                  "description": "<string>",
                                  "arguments": {}
                                }
                              }
                            ],
                            "images": [
                              "<string>"
                            ]
                          },
                          "done": true,
                          "done_reason": "<string>",
                          "total_duration": 123,
                          "load_duration": 123,
                          "prompt_eval_count": 123,
                          "prompt_eval_duration": 123,
                          "eval_count": 123,
                          "eval_duration": 123,
                          "logprobs": [
                            {
                              "token": "<string>",
                              "logprob": 123,
                              "bytes": [
                                123
                              ],
                              "top_logprobs": [
                                {
                                  "token": "<string>",
                                  "logprob": 123,
                                  "bytes": [
                                    123
                                  ]
                                }
                              ]
                            }
                          ]
                        }
             */
            var payload = new
            {
                model = _options.OllamaModel,
                stream = false,
                temperature = temperature,
                messages = new[]
        {
            new
            {
                role = "user",
                content = message
            }
        }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_options.OllamaUrl}/api/chat",
                payload);

            response.EnsureSuccessStatusCode();

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());

            return json.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
       //     return "hmm";
        }
        public async Task<ReadOnlyMemory<float>> CreateEmbeddingFromText(string message)
        {
            //use
            //_options.OllamaUrl+"/api/embed";
            //embedding model: _options.OllamaEmbeddingModel
            /*
                          request:
                {
                    "model": "embeddinggemma",
                    "input": "Why is the sky blue?"
                }
             response:
            {
                "model": "embeddinggemma",
                "embeddings": [
                    [
                        0.010071029,
                        -0.0017594862,
                        0.05007221,
                    ]
                ],
                "total_duration": 14143917,
                "load_duration": 1019500,
                "prompt_eval_count": 8
            }
             */
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

        public async Task<List<float[]>> CreateEmbeddingsFromTexts(List<string> messages)
        {
            var result = new List<float[]>();
            int batchSize = 20; // safe batch size for Ollama to avoid 400 Bad Request

            for (int i = 0; i < messages.Count; i += batchSize)
            {
                var batch = messages.Skip(i).Take(batchSize).ToList();

                var payload = new
                {
                    model = _options.OllamaEmbeddingModel,
                    input = batch
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_options.OllamaUrl}/api/embed",
                    payload);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Ollama embedding failed at batch {i/batchSize}: {response.StatusCode} - {error}");
                }

                using var json = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync());

                var embeddingsArray = json.RootElement.GetProperty("embeddings");

                foreach (var embeddingElement in embeddingsArray.EnumerateArray())
                {
                    result.Add(embeddingElement.EnumerateArray().Select(x => x.GetSingle()).ToArray());
                }
            }

            return result;
        }
    }
}
