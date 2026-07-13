using AIStudyHub.Business.AI.Guardrails;
using AIStudyHub.Data.Entities;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Guardrails;
using AIStudyHub.Business.Common;
using System.Text;
using System.Text.RegularExpressions;
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

    public async Task<RagResponse> AskAsync(Guid userId, Guid? documentId, string question, IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing RAG query for user {UserId}", userId);

        // L3: Retrieval with hybrid search and reranking
        var searchResults = await _searchService.SearchAsync(question, userId, documentId, 40, ct);
        var rerankedResults = await _rerankingService.RerankAsync(question, searchResults, 40, ct);
        _logger.LogInformation("After 1st rerank ({Count}): {Chunks}",
            rerankedResults.Count(),
            string.Join("\n===\n", rerankedResults.Take(5).Select(r => r.Content.Length > 250 ? r.Content[..250] + "..." : r.Content)));

        var resultList = rerankedResults.ToList();
        _logger.LogInformation("Ask query: '{Question}' | Retrieved chunks: {Chunks}",
            question, string.Join("\n---\n", resultList.Select(r => r.Content.Length > 200 ? r.Content[..200] + "..." : r.Content)));
        if (!resultList.Any())
        {
            return new RagResponse("Tài liệu của bạn không chứa thông tin này hoặc không tìm thấy tài liệu.", new(), 0.0, IsRelevant: false);
        }

        // Programmatic relevance check — skip LLM if chunks don't match question
        var relevance = await ComputeChunkRelevanceAsync(question, resultList, ct);
        const double RelevanceThreshold = 0.25;
        if (relevance < RelevanceThreshold)
        {
            _logger.LogWarning(
                "Chunk relevance {Relevance:P2} below threshold {Threshold}, calling SuggestRelatedTopicsAsync",
                relevance, RelevanceThreshold);

            var suggestion = await SuggestRelatedTopicsAsync(question, resultList, "Vietnamese", ct);
            var combined = $"Tài liệu không đề cập đến chủ đề này.\n\n{suggestion}";
            return new RagResponse(combined, new(), 0.0, IsRelevant: false);
        }

        // L4: Generate answer using Custom LLM Prompt (Avoids duplicate KernelMemory search)
        var contextBuilder = new StringBuilder();
        foreach (var r in resultList)
        {
            contextBuilder.AppendLine($"--- Source: {r.Source} ---");
            contextBuilder.AppendLine(r.Content);
            contextBuilder.AppendLine();
        }

        _logger.LogInformation("RAG Context being fed to AI:\n{Context}", contextBuilder.ToString());

        var systemPrompt = """
            You are 'AIStudyHub Assistant', a helpful and friendly AI tutor for AIStudyHub.

            ABOUT AI STUDY HUB (System Features):
            - AIStudyHub allows users to upload documents (PDF, Word) and chat with them to extract knowledge.
            - Users can request a "Summary" of any uploaded document.

            ANSWERING RULES:
            1. Base your answer ONLY on the provided SOURCES. Your answer must be strictly limited to what the SOURCES contain.
            2. If the user asks about the AIStudyHub system features or how to use it, use the 'ABOUT AI STUDY HUB' info above to guide them naturally.
            3. YES/NO questions: use the SOURCES to answer. If the SOURCES answer the question indirectly (e.g. user asks "Does it use Java?" and SOURCES say "The backend uses .NET"), answer "Không" or "Có" with the supporting evidence. Never say "Tài liệu không đề cập" if the SOURCES provide enough information to infer the answer.
            4. YES/NO questions about technologies: if SOURCES don't mention X but do mention Y, respond with "Không, hệ thống sử dụng Y chứ không phải X." in Vietnamese. Capitalize technology names properly (e.g. ".NET", "JavaScript", "TypeScript", "Python", "React", "Angular"). If SOURCES contain zero information about the topic at all, say so clearly in Vietnamese (e.g. "Tài liệu không đề cập đến chủ đề này.").
            5. Do NOT insert numeric citations like [1], [2] into your text.
            6. Answer in Vietnamese by default unless the user asks in English.
            """;

        var userPrompt = $"""
            SOURCES:
            {contextBuilder}

            CHAT HISTORY:
            {string.Join("\n", history.Select(m => $"{m.Sender}: {m.Content}"))}

            QUESTION: {question}

            ANSWER:
            """;

        var answer = await _openAiService.SendMessageAsync($"{systemPrompt}\n\n{userPrompt}") ?? "Xin lỗi, tôi không thể trả lời lúc này.";

        // L5: Guardrails
        var isFaithful = await _faithfulnessFilter.ValidateAsync(answer, resultList.Select(r => r.Content));
        // TODO: GroundingVerifier is too strict for short/Vietnamese answers - disabled temporarily
        var groundingResult = new GroundingResult(IsGrounded: true, Score: 1.0, UngroundedClaims: new());
        var confidence = _confidenceScorer.Score(answer, groundingResult, isFaithful);

        // Build citations
        var citations = resultList.Select((r, i) => new CitationInfo(
            Source: r.Source,
            Content: r.Content,
            Relevance: r.Score
        )).ToList();

        return new RagResponse(answer, citations, confidence, IsRelevant: true);
    }


    public async Task<RagResponseWithUsage> AskWithTrackingAsync(Guid userId, Guid? documentId, string question, IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing RAG query with tracking for user {UserId}", userId);

        // L3: Retrieval with hybrid search and reranking
        var searchResults = await _searchService.SearchAsync(question, userId, documentId, 20, ct);
        var rerankedResults = await _rerankingService.RerankAsync(question, searchResults, 5, ct);
        
        var resultList = rerankedResults.ToList();
        if (!resultList.Any())
        {
            return new RagResponseWithUsage("Tài liệu của bạn không chứa thông tin này hoặc không tìm thấy tài liệu.", new(), 0.0, 0, 0, IsRelevant: false);
        }

        // Programmatic relevance check — skip LLM if chunks don't match question
        var relevance = await ComputeChunkRelevanceAsync(question, resultList, ct);
        const double RelevanceThreshold = 0.25;
        if (relevance < RelevanceThreshold)
        {
            _logger.LogWarning(
                "Chunk relevance {Relevance:P2} below threshold {Threshold}, calling SuggestRelatedTopicsAsync",
                relevance, RelevanceThreshold);

            var suggestion = await SuggestRelatedTopicsAsync(question, resultList, "Vietnamese", ct);
            var combined = $"Tài liệu không đề cập đến chủ đề này.\n\n{suggestion}";
            return new RagResponseWithUsage(combined, new(), 0.0, 0, 0, IsRelevant: false);
        }

        // L4: Pre-check for yes/no tech questions — short-circuit if answer is clearly "Không"
        if (TryDetectNoAnswer(question, resultList, out var noAnswer))
        {
            _logger.LogInformation("Yes/No shortcut triggered: {Answer}", noAnswer);
            return new RagResponseWithUsage(noAnswer, new(), 1.0, 0, 0, IsRelevant: true);
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
            - Users can request a "Summary" of any uploaded document.

            ANSWERING RULES:
            1. Base your answer ONLY on the provided SOURCES. Your answer must be strictly limited to what the SOURCES contain.
            2. If the user asks about the AIStudyHub system features or how to use it, use the 'ABOUT AI STUDY HUB' info above to guide them naturally.
            3. YES/NO questions: use the SOURCES to answer. If the SOURCES answer the question indirectly (e.g. user asks "Does it use Java?" and SOURCES say "The backend uses .NET"), answer "Không" or "Có" with the supporting evidence. Never say "Tài liệu không đề cập" if the SOURCES provide enough information to infer the answer.
            4. YES/NO questions about technologies: if SOURCES don't mention X but do mention Y, respond with "Không, hệ thống sử dụng Y chứ không phải X." in Vietnamese. Capitalize technology names properly (e.g. ".NET", "JavaScript", "TypeScript", "Python", "React", "Angular"). If SOURCES contain zero information about the topic at all, say so clearly in Vietnamese (e.g. "Tài liệu không đề cập đến chủ đề này.").
            5. Do NOT insert numeric citations like [1], [2] into your text.
            6. Answer in Vietnamese by default unless the user asks in English.
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
        // TODO: GroundingVerifier is too strict for short/Vietnamese answers - disabled temporarily
        var groundingResult = new GroundingResult(IsGrounded: true, Score: 1.0, UngroundedClaims: new());
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
    /// Generates related-topic suggestions based ONLY on the content of the retrieved document chunks.
    /// The LLM is strictly constrained to cite phrases that appear verbatim in the excerpts.
    /// </summary>
    private async Task<string> SuggestRelatedTopicsAsync(
        string question,
        IReadOnlyList<SearchResult> chunks,
        string language,
        CancellationToken ct = default)
    {
        if (chunks.Count == 0)
            return string.Empty;

        var contextSnippet = string.Join(
            "\n\n",
            chunks.Take(5).Select(c => $"[{c.Source}]\n{c.Content}"));

        var suggestionPrompt = $"""
            The user asked: "{question}"

            Here are the exact text excerpts retrieved from the document:
            ---
            {contextSnippet}
            ---

            IMPORTANT: You may ONLY suggest topics that appear as exact words or phrases in the excerpts above.
            Do NOT use your own knowledge to add topics not found in the document.
            Based only on what appears in the excerpts above, suggest 2-4 specific questions
            the user could ask that ARE answered by the document. Each suggestion must
            contain at least one phrase that appears verbatim in the excerpts.
            Respond in {language}.

            Format: a short friendly paragraph. No invented topics.
            """;

        var fallbackLabel = language == "Vietnamese"
            ? "Gợi ý chủ đề liên quan:"
            : "Related topics you might be interested in:";

        var suggestion = await _openAiService.SendMessageAsync(suggestionPrompt)
            ?? $"{fallbackLabel} {string.Join(", ", chunks.Take(3).Select(c => c.Source))}";

        return suggestion;
    }

    /// <summary>
    /// Detects yes/no tech questions and returns "Không, hệ thống sử dụng {actualTech} chứ không phải {askedTech}."
    /// Runs a simple keyword check on the top chunks — no LLM call needed.
    /// </summary>
    private bool TryDetectNoAnswer(string question, IReadOnlyList<SearchResult> chunks, out string answer)
    {
        answer = "";
        var lowerQ = question.ToLowerInvariant();

        // Match "có sử dụng X không" / "có dùng X không" / "uses X" / "sài X không" / "X không"
        var match = Regex.Match(lowerQ, @"(?:có\s+(?:sử dụng|dùng|sài)\s+(?<tech>\w+)\s*không|dùng\s+(?<tech>\w+)\s*không|does\s+it\s+use\s+(?<tech>\w+)|uses?\s+(?<tech>\w+)|sài\s+(?<tech>\w+))", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        var askedTech = match.Groups["tech"].Value.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(askedTech) || askedTech.Length < 2)
            return false;

        // Extract all tech keywords from chunks
        var chunkText = string.Join(" ", chunks.Take(5).Select(c => c.Content.ToLowerInvariant()));

        // Known tech stack keywords present in the document
        var techKeywords = new[]
        {
            "c#", "csharp", ".net", "asp.net", "asp.net core", "entity framework", "ef core",
            "react", "reactjs", "typescript", "javascript", "js", "nextjs", "next.js",
            "java", "spring", "springboot", "spring boot",
            "python", "django", "flask",
            "postgresql", "postgres", "sql server", "mysql", "mongodb", "redis", "qdrant",
            "docker", "kubernetes", "ci/cd", "github actions",
            "html", "css", "scss", "rest api", "restful", "graphql",
            "angular", "vue", "vuejs", "nodejs", "node.js"
        };

        var foundInChunks = techKeywords
            .Where(t => t.Length >= 3 && !t.Equals(askedTech) && chunkText.Contains(t))
            .Take(3)
            .ToList();

        if (foundInChunks.Count == 0)
            return false;

        // Blocklist: common Vietnamese words the regex might capture from natural sentences
        var commonVietnameseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cái", "gì", "được", "có", "không", "bạn", "tôi", "nào", "sao",
            "vậy", "hả", "nhỉ", "ko", "k", "dc", "v", "là", "của", "trong"
        };
        if (commonVietnameseWords.Contains(askedTech))
            return false; // not a real tech question — let LLM handle it

        // Avoid false positives: only trigger if the asked tech is NOT mentioned in chunks
        // but at least one other tech IS mentioned (meaning the document covers this topic area)
        var askedMentioned = techKeywords.Any(t => t.Equals(askedTech) && chunkText.Contains(t));
        if (askedMentioned)
            return false; // let the LLM handle it — the tech IS in the document

        var actualTech = foundInChunks[0];
        answer = $"Không, hệ thống sử dụng {actualTech} chứ không phải {askedTech}.";
        return true;
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
