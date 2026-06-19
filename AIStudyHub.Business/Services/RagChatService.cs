using System.Text;
using System.Text.RegularExpressions;
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
        // State 0: greeting / casual message -> normal chat, never consults documents.
        if (IsGreetingOrCasualMessage(request.Message))
        {
            return BuildNormalChatResponse(
                await GenerateGeneralChatAnswerAsync(request.Message));
        }

        // State 1: caller did not provide any documents -> validation message (no LLM call).
        if (request.DocumentIds is null || request.DocumentIds.Count == 0)
        {
            return BuildValidationResponse(
                "Please provide one or more documents to ask a question about them.");
        }

        // Verify each requested id exists and is owned by the caller.
        var requestedIds = request.DocumentIds.Distinct().ToList();
        var existingIds = await _unitOfWork.Documents
            .Query()
            .Where(d => requestedIds.Contains(d.Id) && d.UserId == userId)
            .Select(d => d.Id)
            .ToListAsync();

        if (existingIds.Count != requestedIds.Count)
        {
            return BuildValidationResponse(
                "One or more of the selected documents are invalid or no longer available. Please choose valid documents.");
        }

        // State 2: documents exist. Try to retrieve relevant chunks.
        var relevantChunks = await RetrieveRelevantChunksAsync(
            request.Message, existingIds, userId);

        if (relevantChunks.Count == 0)
        {
            // State 2a: valid docs but no relevant info -> polite "not in document" message.
            return BuildNoInfoResponse(
                await GenerateNoInfoAnswerAsync(request.Message, existingIds));
        }

        // State 2b: valid docs + relevant chunks -> answer from the chunks, no [N] markers.
        var documentIds = relevantChunks.Select(c => c.DocumentId).Distinct().ToList();
        var documentTitles = await GetDocumentTitlesAsync(documentIds);
        var context = BuildContext(relevantChunks, documentTitles);

        var rawAnswer = await GenerateAnswerAsync(request.Message, context);
        var answer = StripInlineCitations(rawAnswer);

        var references = _citationService.CreateReferences(relevantChunks, documentTitles);
        var citations = _citationService.CreateCitations(references);
        var neighbors = BuildNeighbors(relevantChunks, documentTitles);

        return new RagChatResponseDto(answer, citations, references, neighbors);
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
            .Select(c =>
            {
                var chunkLower = (c.ChunkJson ?? "").ToLowerInvariant();
                return new
                {
                    Chunk = c,
                    ChunkLower = chunkLower,
                    Score = queryWords.Count(w => chunkLower.Contains(w))
                };
            })
            .Where(x => x.Score >= 2
                || (x.Score >= 1 && queryWords.Any(w => w.Length >= 4 && x.ChunkLower.Contains(w))))
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
        var systemPrompt = """
            You are a helpful AI assistant that answers questions strictly from the provided sources.

            RULES:
            1. ONLY answer using information explicitly stated in the sources below.
            2. If the sources do not contain the answer, reply exactly:
               "The provided documents do not contain information to answer this question."
            3. Do NOT insert numeric citations like [1], [2], [3] into the answer text.
            4. Write in clear, natural prose.
            5. Keep the answer focused and concise.
            """;

        var userPrompt = $"""
            SOURCES:
            {context}

            QUESTION: {question}

            ANSWER:
            """;

        try
        {
            var response = await _openAiService.SendMessageAsync($"{systemPrompt}\n\n{userPrompt}");
            return (response ?? string.Empty).Trim();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "LLM server connection failed. URL: {Url}", _options.OllamaUrl);
            return "I couldn't generate an answer right now. Please try again.";
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

    private async Task<string> GenerateGeneralChatAnswerAsync(string message)
    {
        var systemPrompt = """
            You are a friendly AI assistant. Respond naturally and conversationally.
            Keep responses concise and helpful.
            """;

        var userPrompt = $"MESSAGE: {message}\n\nRESPONSE:";

        try
        {
            var response = await _openAiService.SendMessageAsync($"{systemPrompt}\n\n{userPrompt}");
            return (response ?? string.Empty).Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM call failed in general-chat path");
            return "Hello! How can I help you today?";
        }
    }

    private async Task<string> GenerateNoInfoAnswerAsync(string question, List<Guid> existingIds)
    {
        var systemPrompt = """
            You are a helpful AI assistant.
            The user asked a question referencing specific document(s), but the documents do not contain
            information to answer it. Respond politely in one or two sentences telling the user that the
            referenced document(s) do not contain the information. Do NOT invent an answer. Do NOT use
            numeric citations.
            """;

        var userPrompt = $"QUESTION: {question}\n\nANSWER:";

        try
        {
            var response = await _openAiService.SendMessageAsync($"{systemPrompt}\n\n{userPrompt}");
            return (response ?? string.Empty).Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM call failed in no-info path");
            return "The provided documents do not contain information to answer this question.";
        }
    }

    private static bool IsGreetingOrCasualMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return true;

        var normalized = message.Trim().ToLowerInvariant();
        var greetings = new HashSet<string>(StringComparer.Ordinal)
        {
            "hi", "hello", "hey", "yo", "hiya", "hii", "hiii",
            "thanks", "thank you", "thx", "ty",
            "bye", "goodbye", "cya", "see ya",
            "ok", "okay", "kk", "cool", "great", "nice",
            "good morning", "good afternoon", "good evening",
            "how are you", "how's it going", "what's up", "sup"
        };

        if (greetings.Contains(normalized)) return true;

        var stripped = normalized.TrimEnd('.', '!', '?', ',', ' ');
        if (greetings.Contains(stripped)) return true;

        var wordCount = stripped.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount <= 2 && !stripped.Contains('?') && stripped.Length <= 20)
            return true;

        return false;
    }

    private static RagChatResponseDto BuildValidationResponse(string message)
        => new(message, new List<CitationDto>(), new List<ReferenceDto>(), new List<NeighborDto>());

    private static RagChatResponseDto BuildNoInfoResponse(string message)
        => new(message, new List<CitationDto>(), new List<ReferenceDto>(), new List<NeighborDto>());

    private static RagChatResponseDto BuildNormalChatResponse(string message)
        => new(message, new List<CitationDto>(), new List<ReferenceDto>(), new List<NeighborDto>());

    private static string StripInlineCitations(string answer)
    {
        if (string.IsNullOrEmpty(answer)) return answer;
        return Regex.Replace(answer, @"\s*\[\s*\d+(?:\s*,\s*\d+)*\s*\]", string.Empty).Trim();
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
