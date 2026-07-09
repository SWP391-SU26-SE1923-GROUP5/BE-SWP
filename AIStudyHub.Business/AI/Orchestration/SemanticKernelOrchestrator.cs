using AIStudyHub.Business.AI.Guardrails;
using AIStudyHub.Data.Entities;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Guardrails;
using AIStudyHub.Business.Common;
using System.Text;
using AIStudyHub.Business.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.AI.Orchestration;

public class SemanticKernelOrchestrator : ISemanticKernelOrchestrator
{
    private readonly IHybridSearchService _searchService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IRerankingService _rerankingService;
    private readonly IFaithfulnessFilter _faithfulnessFilter;
    private readonly IGroundingVerifier _groundingVerifier;
    private readonly IConfidenceScorer _confidenceScorer;
    private readonly SemanticKernelOptions _options;
    private readonly IOpenAIService _openAiService;
    private readonly ILogger<SemanticKernelOrchestrator> _logger;

    public SemanticKernelOrchestrator(
        IHybridSearchService searchService,
        IVectorStoreService vectorStoreService,
        IRerankingService rerankingService,
        IFaithfulnessFilter faithfulnessFilter,
        IGroundingVerifier groundingVerifier,
        IConfidenceScorer confidenceScorer,
        IOptions<SemanticKernelOptions> options,
        IOpenAIService openAiService,
        ILogger<SemanticKernelOrchestrator> logger)
    {
        _searchService = searchService;
        _vectorStoreService = vectorStoreService;
        _rerankingService = rerankingService;
        _faithfulnessFilter = faithfulnessFilter;
        _groundingVerifier = groundingVerifier;
        _confidenceScorer = confidenceScorer;
        _options = options.Value;
        _openAiService = openAiService;
        _logger = logger;
    }

    //public async Task<RagResponse> AskAsync(Guid userId, Guid? documentId, string question, IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
    //{
    //    _logger.LogInformation("Processing RAG query for user {UserId}", userId);

    //    // L3: Retrieval with hybrid search and reranking
    //    var searchResults = await _searchService.SearchAsync(question, userId, documentId, 40, ct);
    //    var rerankedResults = await _rerankingService.RerankAsync(question, searchResults, 40, ct);
    //    _logger.LogInformation("After 1st rerank ({Count}): {Chunks}",
    //        rerankedResults.Count(),
    //        string.Join("\n===\n", rerankedResults.Take(5).Select(r => r.Content.Length > 250 ? r.Content[..250] + "..." : r.Content)));
        
    //    var resultList = rerankedResults.ToList();
    //    _logger.LogInformation("Ask query: '{Question}' | Retrieved chunks: {Chunks}",
    //        question, string.Join("\n---\n", resultList.Select(r => r.Content.Length > 200 ? r.Content[..200] + "..." : r.Content)));
    //    if (!resultList.Any())
    //    {
    //        return new RagResponse("Tài liệu của bạn không chứa thông tin này hoặc không tìm thấy tài liệu.", new(), 0.0, IsRelevant: false);
    //    }

    //    // Programmatic relevance check — skip LLM if chunks don't match question
    //    var relevance = await ComputeChunkRelevanceAsync(question, resultList, ct);
    //    const double RelevanceThreshold = 0.15;
    //    if (relevance < RelevanceThreshold)
    //    {
    //        _logger.LogWarning(
    //            "Chunk relevance {Relevance:P2} below threshold {Threshold}, returning fallback",
    //            relevance, RelevanceThreshold);
    //        return new RagResponse("Tài liệu của bạn không chứa thông tin này.", new(), 0.0, IsRelevant: false);
    //    }

    //    // L4: Generate answer using Custom LLM Prompt (Avoids duplicate KernelMemory search)
    //    var contextBuilder = new StringBuilder();
    //    foreach (var r in resultList)
    //    {
    //        contextBuilder.AppendLine($"--- Source: {r.Source} ---");
    //        contextBuilder.AppendLine(r.Content);
    //        contextBuilder.AppendLine();
    //    }

    //    _logger.LogInformation("RAG Context being fed to AI:\n{Context}", contextBuilder.ToString());

    //    var systemPrompt = """
    //        You are 'AIStudyHub Assistant', a helpful and friendly AI tutor for AIStudyHub.

    //        ABOUT AI STUDY HUB (System Features):
    //        - AIStudyHub allows users to upload documents (PDF, Word) and chat with them to extract knowledge.
    //        - Users can automatically generate "Flashcards" from their documents to study.
    //        - Users can automatically generate "Quizzes" (Multiple-Choice) to test their knowledge.
    //        - Users can request a "Summary" of any uploaded document.

    //        ANSWERING RULES:
    //        1. If the user asks about the content of their uploaded document, base your answer on the provided SOURCES. If the SOURCES do not contain the answer, say so honestly.
    //        2. If the user asks about the AIStudyHub system features or how to use it, use the 'ABOUT AI STUDY HUB' info above to guide them naturally.
    //        3. If the SOURCES above do not contain enough information, answer general questions (e.g., software architecture, programming concepts, technology) using your own knowledge.
    //        4. Do NOT insert numeric citations like [1], [2] into your text.
    //        5. Answer in Vietnamese by default unless the user asks in English.
    //        """;

    //    var userPrompt = $"""
    //        SOURCES:
    //        {contextBuilder}

    //        CHAT HISTORY:
    //        {string.Join("\n", history.Select(m => $"{m.Sender}: {m.Content}"))}

    //        QUESTION: {question}

    //        ANSWER:
    //        """;

    //    var answer = await _openAiService.SendMessageAsync($"{systemPrompt}\n\n{userPrompt}") ?? "Xin lỗi, tôi không thể trả lời lúc này.";

    //    // L5: Guardrails
    //    var isFaithful = await _faithfulnessFilter.ValidateAsync(answer, resultList.Select(r => r.Content));
    //    var groundingResult = await _groundingVerifier.VerifyAsync(answer, resultList);
    //    var confidence = _confidenceScorer.Score(answer, groundingResult, isFaithful);

    //    // Build citations
    //    var citations = resultList.Select((r, i) => new CitationInfo(
    //        Source: r.Source,
    //        Content: r.Content,
    //        Relevance: r.Score
    //    )).ToList();

    //    return new RagResponse(answer, citations, confidence, IsRelevant: true);
    //}

    
    public async Task<RagResponseWithUsage> AskWithTrackingAsync(Guid userId, Guid? documentId, string question, IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing RAG query with tracking for user {UserId}", userId);

        // L3: Retrieval with hybrid search and reranking
        var searchResults = await _searchService.SearchAsync(question, userId, documentId, 20, ct);
        var rerankedResults = await _rerankingService.RerankAsync(question, searchResults, 10, ct);
        
        var resultList = rerankedResults.ToList();
        if (!resultList.Any())
        {
            return new RagResponseWithUsage("Tài liệu của bạn không chứa thông tin này hoặc không tìm thấy tài liệu.", new(), 0.0, 0, 0, IsRelevant: false);
        }

        // Programmatic relevance check — skip LLM if chunks don't match question
        var relevance = await ComputeChunkRelevanceAsync(question, resultList, ct);
        const double RelevanceThreshold = 0.15;
        if (relevance < RelevanceThreshold)
        {
            _logger.LogWarning(
                "Chunk relevance {Relevance:P2} below threshold {Threshold}, returning fallback",
                relevance, RelevanceThreshold);
            return new RagResponseWithUsage(
                "Tài liệu của bạn không chứa thông tin này.",
                new(), 0.0, 0, 0, IsRelevant: false);
        }

        // L4: Generate answer using Custom LLM Prompt
        var contextBuilder = new StringBuilder();
        foreach (var r in resultList)
        {
            contextBuilder.AppendLine($"--- Source: {r.Source} ---");
            contextBuilder.AppendLine(r.Content);
            contextBuilder.AppendLine();
        }

        var systemPrompt = """
            You are 'AIStudyHub Assistant', a helpful and friendly AI tutor for AIStudyHub.

            ABOUT AI STUDY HUB (System Features):
            - AIStudyHub allows users to upload documents (PDF, Word) and chat with them to extract knowledge.
            - Users can automatically generate "Flashcards" from their documents to study.
            - Users can automatically generate "Quizzes" (Multiple-Choice) to test their knowledge.
            - Users can request a "Summary" of any uploaded document.

            ANSWERING RULES:
            1. If the user asks about the content of their uploaded document, base your answer on the provided SOURCES. If the SOURCES do not contain the answer, say so honestly.
            2. If the user asks about the AIStudyHub system features or how to use it, use the 'ABOUT AI STUDY HUB' info above to guide them naturally.
            3. If the SOURCES above do not contain enough information, answer general questions (e.g., software architecture, programming concepts, technology) using your own knowledge.
            4. Do NOT insert numeric citations like [1], [2] into your text.
            5. Answer in Vietnamese by default unless the user asks in English.
            """;

        var userPrompt = $"""
            SOURCES:
            {contextBuilder}

            CHAT HISTORY:
            {string.Join("\n", history.Select(m => $"{m.Sender}: {m.Content}"))}

            QUESTION: {question}

            ANSWER:
            """;

        var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";
        var usageResult = await _openAiService.SendMessageWithUsageAsync(fullPrompt);
        var answer = usageResult.Text ?? "Xin lỗi, tôi không thể trả lời lúc này.";

        // L5: Guardrails
        var isFaithful = await _faithfulnessFilter.ValidateAsync(answer, resultList.Select(r => r.Content));
        var groundingResult = await _groundingVerifier.VerifyAsync(answer, resultList);
        var confidence = _confidenceScorer.Score(answer, groundingResult, isFaithful);

        // Build citations
        var citations = resultList.Select((r, i) => new CitationInfo(
            Source: r.Source,
            Content: r.Content,
            Relevance: r.Score
        )).ToList();

        return new RagResponseWithUsage(answer, citations, confidence, usageResult.InputTokens, usageResult.OutputTokens, IsRelevant: true);
    }

    public async Task<string> SummarizeAsync(Guid documentId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("SummarizeAsync START: documentId={DocumentId}, userId={UserId}", documentId, userId);

        var payloads = await _vectorStoreService.GetPayloadsByDocumentIdAsync(documentId);

        if (payloads.Count == 0)
        {
            return "Không tìm thấy nội dung tài liệu để tóm tắt.";
        }

        var sortedChunks = payloads
            .OrderBy(p => int.TryParse(p.GetValueOrDefault("chunkIndex", "0"), out var idx) ? idx : 0)
            .Select(p => FixMojibake(p.GetValueOrDefault("text", "")))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        var documentContent = string.Join("\n\n", sortedChunks);

        if (string.IsNullOrWhiteSpace(documentContent))
        {
            return "Tài liệu không có văn bản.";
        }

        var systemPrompt = "Bạn là trợ lý ảo giúp tóm tắt nội dung tài liệu. Hãy tóm tắt văn bản dưới đây một cách ngắn gọn, súc tích và bao quát những ý chính nhất.";
        var userPrompt = $"VĂN BẢN TÀI LIỆU:\n{documentContent}\n\nYÊU CẦU: Hãy tóm tắt nội dung chính của tài liệu trên.";

        return await _openAiService.SendMessageAsync($"{systemPrompt}\n\n{userPrompt}") ?? "Không thể tóm tắt tài liệu.";
    }

    public async Task<SummarizeResult> SummarizeWithTrackingAsync(Guid documentId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("SummarizeWithTrackingAsync START: documentId={DocumentId}, userId={UserId}", documentId, userId);

        // Fetch all chunks from Qdrant for this document
        var payloads = await _vectorStoreService.GetPayloadsByDocumentIdAsync(documentId);

        if (payloads.Count == 0)
        {
            return new SummarizeResult("Không tìm thấy nội dung tài liệu để tóm tắt.", 0, 0);
        }

        // Sort chunks by index if possible
        var sortedChunks = payloads
            .OrderBy(p => int.TryParse(p.GetValueOrDefault("chunkIndex", "0"), out var idx) ? idx : 0)
            .Select(p => FixMojibake(p.GetValueOrDefault("text", "")))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        var documentContent = string.Join("\n\n", sortedChunks);

        if (string.IsNullOrWhiteSpace(documentContent))
        {
            return new SummarizeResult("Tài liệu không có văn bản.", 0, 0);
        }

        var systemPrompt = "Bạn là trợ lý ảo giúp tóm tắt nội dung tài liệu. Hãy tóm tắt văn bản dưới đây một cách ngắn gọn, súc tích và bao quát những ý chính nhất.";
        var userPrompt = $"VĂN BẢN TÀI LIỆU:\n{documentContent}\n\nYÊU CẦU: Hãy tóm tắt nội dung chính của tài liệu trên.";

        var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";
        var usageResult = await _openAiService.SendMessageWithUsageAsync(fullPrompt);
        var summary = usageResult.Text ?? "Không thể tóm tắt tài liệu.";

        return new SummarizeResult(summary, usageResult.InputTokens, usageResult.OutputTokens);
    }

    /// <summary>
    /// Computes a relevance score between the user's question and the top retrieved chunks,
    /// without making an extra LLM call. Uses 60% keyword overlap + 40% average Qdrant chunk score.
    /// Returns a value in [0, 1]. Values below RelevanceThreshold indicate the chunks
    /// don't match the question, so the LLM call should be skipped with a "no info" fallback.
    /// </summary>
    private async Task<double> ComputeChunkRelevanceAsync(
        string question,
        IReadOnlyList<SearchResult> topChunks,
        CancellationToken ct = default)
    {
        var questionWords = question.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet();

        if (questionWords.Count == 0)
            return 1.0;

        var chunkText = string.Join(" ",
            topChunks.Take(3).Select(c => c.Content.ToLowerInvariant()));

        var matchedWords = questionWords.Count(qw =>
            chunkText.Contains(qw, StringComparison.Ordinal));

        var avgChunkScore = topChunks.Take(3).Average(c => c.Score);
        var keywordScore = (double)matchedWords / questionWords.Count;
        var relevance = keywordScore * 0.6 + Math.Min(avgChunkScore, 1.0) * 0.4;

        _logger.LogInformation(
            "Chunk relevance: {Relevance:P2} (keyword={KeywordScore:P2}, chunkScore={ChunkScore:P2})",
            relevance, keywordScore, avgChunkScore);

        await Task.CompletedTask;
        return relevance;
    }

    /// <summary>
    /// Fixes mojibake (UTF-8 bytes misread as Latin-1) commonly found in PDF-extracted Vietnamese text.
    /// </summary>
    private static string FixMojibake(string input) => TextSanitizer.FixMojibake(input);
}
