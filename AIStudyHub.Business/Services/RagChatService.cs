using System.Text;
using System.Text.Json;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Services;

public sealed class RagChatService : IRagChatService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ICitationService _citationService;
    private readonly HttpClient _llmClient;
    private readonly RagOptions _options;
    private readonly ILogger<RagChatService> _logger;

    public RagChatService(
        IUnitOfWork unitOfWork,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ICitationService citationService,
        IHttpClientFactory httpClientFactory,
        IOptions<RagOptions> options,
        ILogger<RagChatService> logger)
    {
        _unitOfWork = unitOfWork;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _citationService = citationService;
        _llmClient = httpClientFactory.CreateClient("LlmClient");
        _options = options.Value;
        _logger = logger;

        _llmClient.BaseAddress = new Uri(_options.Gpt4AllUrl);
    }

    public async Task<RagChatResponseDto> ChatAsync(RagChatRequestDto request, Guid userId)
    {
        var relevantChunks = await RetrieveRelevantChunksAsync(request.Message, request.DocumentIds, userId);

        if (relevantChunks.Count == 0)
        {
            return new RagChatResponseDto(
                "I couldn't find any relevant documents to answer your question. Please upload some documents first.",
                new List<CitationDto>(),
                new List<ReferenceDto>()
            );
        }

        var documentIds = relevantChunks.Select(c => c.DocumentId).Distinct().ToList();
        var documentTitles = await GetDocumentTitlesAsync(documentIds);
        var references = _citationService.CreateReferences(relevantChunks, documentTitles);

        var context = BuildContext(relevantChunks, documentTitles);
        var answer = await GenerateAnswerAsync(request.Message, context);

        var citations = _citationService.CreateCitations(references);
        var formattedAnswer = _citationService.FormatAnswerWithCitations(answer, references);

        return new RagChatResponseDto(
            formattedAnswer,
            citations,
            references
        );
    }

    private async Task<List<ChunkDto>> RetrieveRelevantChunksAsync(
        string query, List<Guid>? documentIds, Guid userId)
    {
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

        var searchResults = await _vectorStoreService.SearchAsync(
            queryEmbedding,
            _options.TopKChunks,
            documentIds?.Count > 0 ? new Dictionary<string, string> { ["userId"] = userId.ToString() } : null);

        if (searchResults.Count == 0)
        {
            return await GetChunksFromDatabaseAsync(query, documentIds, userId);
        }

        var chunkIds = searchResults
            .Where(r => r.Metadata.TryGetValue("chunkId", out _))
            .Select(r => r.Metadata["chunkId"])
            .ToList();

        var chunks = await _unitOfWork.DocumentChunks
            .Query()
            .Include(c => c.Document)
            .Where(c => chunkIds.Contains(c.Id.ToString()))
            .AsNoTracking()
            .ToListAsync();

        var orderedChunks = chunkIds
            .Select(id => chunks.FirstOrDefault(c => c.Id.ToString() == id))
            .Where(c => c != null)
            .Select(c => new ChunkDto(
                c!.Id,
                c.DocumentId,
                c.ChunkJson ?? "",
                0,
                null))
            .ToList();

        return orderedChunks;
    }

    private async Task<List<ChunkDto>> GetChunksFromDatabaseAsync(
        string query, List<Guid>? documentIds, Guid userId)
    {
        var queryable = _unitOfWork.DocumentChunks
            .Query()
            .Include(c => c.Document)
            .AsNoTracking();

        if (documentIds?.Count > 0)
        {
            queryable = queryable.Where(c => documentIds.Contains(c.DocumentId));
        }

        var allChunks = await queryable.ToListAsync();

        if (allChunks.Count == 0)
            return new List<ChunkDto>();

        var queryWords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var scoredChunks = allChunks
            .Select(c => new
            {
                Chunk = c,
                Score = queryWords.Count(w => (c.ChunkJson ?? "").ToLowerInvariant().Contains(w))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Chunk.CreatedAt)
            .Take(_options.TopKChunks)
            .ToList();

        return scoredChunks
            .Select(x => new ChunkDto(
                x.Chunk.Id,
                x.Chunk.DocumentId,
                x.Chunk.ChunkJson ?? "",
                0,
                null))
            .ToList();
    }

    private static string BuildContext(List<ChunkDto> chunks, Dictionary<Guid, string> documentTitles)
    {
        var context = new StringBuilder();
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var docTitle = documentTitles.GetValueOrDefault(chunk.DocumentId, "Unknown");
            context.AppendLine($"--- Source {i + 1}: {docTitle} ---");
            context.AppendLine(chunk.Content);
            context.AppendLine();
        }
        return context.ToString();
    }

    private async Task<string> GenerateAnswerAsync(string question, string context)
    {
        try
        {
            var systemPrompt = """
                You are a helpful AI assistant specialized in answering questions based on provided documents.
                
                IMPORTANT RULES:
                1. ONLY answer questions using information from the provided sources.
                2. If the answer is not found in the sources, clearly state: "I don't have enough information in the provided documents to answer this question."
                3. Use [1], [2], [3] etc. to cite sources inline where you use information.
                4. Be concise but thorough in your answers.
                5. Always attribute information to the correct source number.
                """;

            var userPrompt = $"""
                CONTEXT (Sources):
                {context}

                ---

                QUESTION: {question}

                ANSWER (with citations like [1], [2], [3]):
                """;

            var payload = new
            {
                model = _options.Gpt4AllModel,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = _options.MaxTokens,
                temperature = _options.Temperature
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _llmClient.PostAsync("/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("LLM request failed: {Error}", error);
                return "I encountered an error generating a response. Please try again.";
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                return choices[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "I couldn't generate a response.";
            }

            return "I couldn't generate a response.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "LLM server connection failed. URL: {Url}", _options.Gpt4AllUrl);
            return $"I couldn't connect to the AI server at {_options.Gpt4AllUrl}. Please ensure the local AI server is running.";
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != CancellationToken.None)
        {
            _logger.LogWarning("LLM request timed out");
            return "The request timed out. Please try with a shorter question.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during LLM request");
            return "I'm sorry, but I'm having trouble generating a response right now. Please try again.";
        }
    }

    private async Task<Dictionary<Guid, string>> GetDocumentTitlesAsync(List<Guid> documentIds)
    {
        var documents = await _unitOfWork.Documents
            .Query()
            .Where(d => documentIds.Contains(d.Id))
            .AsNoTracking()
            .ToListAsync();

        return documents.ToDictionary(d => d.Id, d => d.Title);
    }
}
