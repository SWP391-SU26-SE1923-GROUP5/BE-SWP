using System.Text;
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
    private readonly ILocalAIService _openAiService;

    public RagChatService(
        IUnitOfWork unitOfWork,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ICitationService citationService,
        IHttpClientFactory httpClientFactory,
        ILocalAIService openAIService,
        IOptions<RagOptions> options,
        ILogger<RagChatService> logger)
    {
        _unitOfWork = unitOfWork;
        _openAiService = openAIService;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _citationService = citationService;
        _llmClient = httpClientFactory.CreateClient("LlmClient");
        _options = options.Value;
        _logger = logger;

        _llmClient.BaseAddress = new Uri(_options.OllamaUrl);
    }

    public async Task<RagChatResponseDto> ChatAsync(RagChatRequestDto request, Guid userId)
    {
        var relevantChunks = await RetrieveRelevantChunksAsync(request.Message, request.DocumentIds, userId);

        if (relevantChunks.Count == 0)
        {
            return new RagChatResponseDto(
                "I couldn't find any relevant documents to answer your question. Please upload some documents first.",
                new List<CitationDto>(),
                new List<ReferenceDto>(),
                new List<NeighborDto>()
            );
        }

        var documentIds = relevantChunks.Select(c => c.DocumentId).Distinct().ToList();
        var documentTitles = await GetDocumentTitlesAsync(documentIds);
        var references = _citationService.CreateReferences(relevantChunks, documentTitles);

        var context = BuildContext(relevantChunks, documentTitles);
        var answer = await GenerateAnswerAsync(request.Message, context);

        var citations = _citationService.CreateCitations(references);
        var formattedAnswer = _citationService.FormatAnswerWithCitations(answer, references);

        var neighbors = BuildNeighbors(relevantChunks, documentTitles);

        return new RagChatResponseDto(
            formattedAnswer,
            citations,
            references,
            neighbors
        );
    }

    public async Task<string> SummarizeAsync(Guid documentId, Guid userId)
    {
        var document = await _unitOfWork.Documents
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

        if (document == null)
            return "Document not found.";

        var chunks = await _unitOfWork.DocumentChunks
            .Query()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.OrderIndex)
            .AsNoTracking()
            .ToListAsync();

        if (chunks.Count == 0)
            return "No content found in this document.";

        var context = new StringBuilder();
        context.AppendLine($"Document: {document.Title}");
        context.AppendLine();
        foreach (var chunk in chunks)
        {
            context.AppendLine(chunk.ChunkJson);
            context.AppendLine();
        }

        var systemPrompt = """
            You are a helpful AI assistant that summarizes documents.
            Provide a clear, concise summary that covers the main points of the document.
            Structure the summary with key topics and their details.
            """;

        var userPrompt = $"""
            Please summarize the following document:

            {context}

            SUMMARY:
            """;

        try
        {
            return await _openAiService.SendMessageAsync($"{systemPrompt}\n\n{userPrompt}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "LLM server connection failed during summarization. URL: {Url}", _options.OllamaUrl);
            return $"I couldn't connect to the AI server at {_options.OllamaUrl}. Please ensure the local AI server is running.";
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Summarization request timed out");
            return "The summarization request timed out. Please try again.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during summarization");
            return "I'm sorry, but I'm having trouble summarizing the document right now. Please try again.";
        }
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

        var resultDict = searchResults
            .Where(r => r.Metadata.TryGetValue("chunkId", out _))
            .ToDictionary(
                r => r.Metadata["chunkId"],
                r => r.Score);

        var orderedChunks = chunkIds
            .Select(id => chunks.FirstOrDefault(c => c.Id.ToString() == id))
            .Where(c => c != null)
            .Select(c => new ChunkDto(
                c!.Id,
                c.DocumentId,
                c.ChunkJson ?? "",
                0,
                null,
                resultDict.TryGetValue(c.Id.ToString(), out var score) ? score : 0.0))
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
                null,
                x.Score))
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

    private static List<NeighborDto> BuildNeighbors(List<ChunkDto> chunks, Dictionary<Guid, string> documentTitles)
    {
        if (chunks.Count == 0)
            return new List<NeighborDto>();

        var maxScore = chunks.Max(c => c.Score);
        if (maxScore == 0)
            maxScore = 1;

        var neighbors = chunks
            .GroupBy(c => c.DocumentId)
            .Select(g =>
            {
                var topChunk = g.OrderByDescending(c => c.Score).First();
                var docTitle = documentTitles.GetValueOrDefault(g.Key, "Unknown");
                return new NeighborDto(
                    docTitle,
                    Math.Round(topChunk.Score, 4),
                    GetNeighborRelevanceLabel(topChunk.Score, maxScore));
            })
            .OrderByDescending(n => n.Score)
            .ToList();

        return neighbors;
    }

    private static string GetNeighborRelevanceLabel(double score, double maxScore)
    {
        if (maxScore <= 0)
            return "Unknown";
        var ratio = score / maxScore;
        return ratio switch
        {
            >= 0.9 => "Highly Relevant",
            >= 0.7 => "Relevant",
            >= 0.5 => "Somewhat Relevant",
            >= 0.3 => "Loosely Relevant",
            _ => "Weakly Relevant"
        };
    }

    public async Task<string> SendRawPromptAsync(string prompt, float temperature = 0.2f)
    {
        try
        {
            return await _openAiService.SendMessageAsync(prompt, temperature);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "LLM server connection failed. URL: {Url}", _options.OllamaUrl);
            throw;
        }
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

            var response = await _openAiService.SendMessageAsync($"{systemPrompt}\n\n{userPrompt}");

            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "LLM server connection failed. URL: {Url}", _options.OllamaUrl);
            return $"I couldn't connect to the AI server at {_options.OllamaUrl}. Please ensure the local AI server is running.";
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
