//using AIStudyHub.Business.Interfaces.Services;
//using AIStudyHub.Business.Options;
//using Microsoft.Extensions.Options;
//using OpenAI;
//using OpenAI.Chat;
//using OpenAI.Embeddings;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace AIStudyHub.Business.Services
//{
//    public class OpenAIService : IOpenAIService
//    {
//        private readonly RagOptions _options;

//        public OpenAIService(IOptions<RagOptions> options)
//        {
//            _options = options.Value;
//        }
//        public async Task<string> SendMessageAsync(string message)
//        {
//            if (string.IsNullOrWhiteSpace(_options.OpenAIApiKey))
//            {
//                throw new InvalidOperationException("The API is either empty or not setup correctly");
//            }
//            try
//            {
//                ChatClient client = new(model: _options.OpenAIChatModel, apiKey: _options.OpenAIApiKey);

//                ChatCompletion completion = await client.CompleteChatAsync(message);
//                string text = completion.Content[0].Text;
//                return text; // Placeholder response
//            }
//            catch(Exception e)
//            {
//                throw new InvalidOperationException("The API is either empty or not setup correctly");
//            }
//        }
//        public async Task<ReadOnlyMemory<float>> CreateEmbeddingFromText(string message)
//        {
//            if (string.IsNullOrWhiteSpace(_options.OpenAIApiKey))
//            {
//                throw new InvalidOperationException("The API is either empty or not setup correctly");
//            }
//            try
//            {
//                EmbeddingClient embeddingClient = new(model: _options.OpenAIEmbeddingModel, apiKey: _options.OpenAIApiKey);

//                var embedding = await embeddingClient.GenerateEmbeddingAsync(message);
//                return embedding.Value.ToFloats();
//            }
//            catch(Exception e)
//            {
//                throw new InvalidOperationException("The API is either empty or not setup correctly");
//            }
//        }

//        }
//    }
