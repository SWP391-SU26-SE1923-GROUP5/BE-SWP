using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.AI.Chat;
using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Tracking;
using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.AI.Chat;

public sealed class AIChatService : IAIChatService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IOpenAIService _openAIService;
    private readonly ISemanticKernelOrchestrator _orchestrator;
    private readonly ITokenTrackerService _tokenTracker;
    private readonly ILogger<AIChatService> _logger;

    private const int EstimatedChatTokens = 1500;

    public AIChatService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IOpenAIService openAiService,
        ISemanticKernelOrchestrator orchestrator,
        ITokenTrackerService tokenTracker,
        ILogger<AIChatService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _openAIService = openAiService;
        _orchestrator = orchestrator;
        _tokenTracker = tokenTracker;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync()
    {
        var sessions = await _unitOfWork.ChatSessions
            .Query()
            .Include(session => session.User)
            .AsNoTracking()
            .OrderByDescending(session => session.CreatedAt)
            .ToListAsync();

        return sessions.Select(_mapper.Map<ChatSessionResponseDto>).ToList();
    }

    public async Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request, Guid userId)
    {
        var session = new ChatSession
        {
            UserId = userId,
            SessionTitle = request.SessionTitle
        };
        await _unitOfWork.ChatSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync();

        var created = await _unitOfWork.ChatSessions
            .Query()
            .Include(chatSession => chatSession.User)
            .AsNoTracking()
            .FirstAsync(chatSession => chatSession.Id == session.Id);

        return _mapper.Map<ChatSessionResponseDto>(created);
    }

    public async Task<ChatSessionResponseDto> UpdateSessionAsync(
        Guid sessionId,
        UpdateChatSessionRequestDto request,
        Guid userId,
        CancellationToken ct = default)
    {
        var title = request.SessionTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 64)
        {
            throw new ArgumentException("Session title must contain between 1 and 64 characters.", nameof(request));
        }

        var session = await _unitOfWork.ChatSessions.Query()
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, ct);
        if (session is null)
        {
            throw new KeyNotFoundException($"Chat session with ID {sessionId} not found.");
        }

        session.SessionTitle = title;
        await _unitOfWork.SaveChangesAsync(ct);

        return _mapper.Map<ChatSessionResponseDto>(session);
    }

    public async Task DeleteSessionAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.ChatSessions.Query()
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, ct);
        if (session is null)
        {
            throw new KeyNotFoundException($"Chat session with ID {sessionId} not found.");
        }

        _unitOfWork.ChatSessions.Remove(session);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var sessionExists = await _unitOfWork.ChatSessions.Query()
            .AnyAsync(session => session.Id == sessionId && session.UserId == userId, ct);
        if (!sessionExists)
        {
            throw new KeyNotFoundException($"Chat session with ID {sessionId} not found.");
        }

        var messages = await _unitOfWork.ChatMessages
            .Query()
            .Where(message => message.ChatSessionId == sessionId)
            .Include(message => message.Citations)
            .OrderBy(message => message.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return messages.Select(_mapper.Map<ChatMessageResponseDto>).ToList();
    }

    public async Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request, Guid userId, CancellationToken ct = default)
    {
        // Check AI token quota before processing
        if (!await _tokenTracker.HasQuotaAsync(userId, EstimatedChatTokens, ct))
        {
            var (current, limit) = await _tokenTracker.GetUsageInfoAsync(userId, ct);
            throw new QuotaExceededException(current, limit, EstimatedChatTokens);
        }

        ChatSession? session;
        if (!request.SessionId.HasValue)
        {
            var title = request.Message.Length > 50 ? request.Message.Substring(0, 47) + "..." : request.Message;
            session = new ChatSession
            {
                UserId = userId,
                SessionTitle = title
            };
            await _unitOfWork.ChatSessions.AddAsync(session, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        else
        {
            session = await _unitOfWork.ChatSessions.GetByIdAsync(request.SessionId.Value, ct);
            if (session is null || session.UserId != userId)
            {
                throw new KeyNotFoundException($"Chat session with ID {request.SessionId} not found or access denied.");
            }
        }

        var userMessage = new ChatMessage
        {
            ChatSessionId = session.Id,
            Sender = "user",
            Content = request.Message
        };
        await _unitOfWork.ChatMessages.AddAsync(userMessage, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var history = await _unitOfWork.ChatMessages
            .Query()
            .Where(m => m.ChatSessionId == session.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
        history.Reverse();

        // Use all documents attached to the session
        var sessionDocs = await _unitOfWork.ChatSessionDocuments
                .Query()
                .Where(x => x.ChatSessionId == session.Id)
                .Select(x => x.DocumentId)
                .ToListAsync(ct);
        IReadOnlyList<Guid>? docIds = sessionDocs.Count > 0 ? sessionDocs : null;

        string aiResponse;
        int inputTokens = 0;
        int outputTokens = 0;
        bool isRelevant = false;
        IReadOnlyList<CitationInfo> ragCitations = Array.Empty<CitationInfo>();
        Dictionary<Guid, string> docIdToFileName = new();

        if (docIds != null && docIds.Count > 0)
        {
            var ragResponse = await _orchestrator.AskWithTrackingAsync(userId, docIds, request.Message, history, ct);
            aiResponse = ragResponse.Answer;
            inputTokens = ragResponse.InputTokens;
            outputTokens = ragResponse.OutputTokens;
            isRelevant = ragResponse.IsRelevant;
            ragCitations = ragResponse.Citations;
            // Step 1: fetch real file names from DB so we verify against actual file names
            var citationDocIds = ragCitations.Select(c => c.DocumentId).Distinct().ToList();
            var docNames = await _unitOfWork.Documents.Query()
                .Where(d => citationDocIds.Contains(d.Id))
                .Select(d => new { d.Id, d.FileName })
                .AsNoTracking()
                .ToListAsync(ct);
            docIdToFileName = docNames.ToDictionary(x => x.Id, x => x.FileName ?? "Tài liệu");

            // Step 2: normalize AI response for citation lookup
            var normalizedResponse = aiResponse.ToLowerInvariant();
            var isExhaustive = RagContextExpander.IsExhaustiveQuery(request.Message);

            // Pre-compute response keywords once; used for both per-chunk match and ranking.
            var responseWords = normalizedResponse
                .Split(new[] { ' ', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 4)
                .Distinct()
                .ToList();

            // Penalize only chunks whose first line is a clear TOC entry.
            // 'Tóm tắt', 'SUMMARY', 'giới thiệu', 'introduction' are NOT auto-rejected
            // because they often hold the actual answer (e.g. team-member list, requirements list).
            static bool IsTocOrSummary(CitationInfo c)
            {
                var content = (c.Content ?? "").TrimStart();
                if (string.IsNullOrWhiteSpace(content)) return true;
                var firstLineEnd = content.IndexOfAny(new[] { '\n', '\r' });
                var firstLine = firstLineEnd > 0 ? content[..firstLineEnd] : content;
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                        firstLine,
                        @"^(Mục\s?lục|TABLE\s?OF\s?CONTENTS)\b",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return false;
                return firstLine.Length < 200;
            }

            // Step 3: keep only citations whose real file name appears in the response,
            // or whose content shares keywords with the response.
            // Exhaustive queries already fall back to keyword match; non-exhaustive queries
            // now also fall back to content-keyword match when relevance was high enough
            // to reach the LLM (otherwise Step 3 would strip every citation for chunk-rich
            // answers that don't mention the file name by literal string).
            ragCitations = ragCitations
                .Where(c =>
                {
                    if (string.IsNullOrWhiteSpace(c.Source)) return false;
                    var realFileName = docIdToFileName.GetValueOrDefault(c.DocumentId, c.Source);
                    if (string.IsNullOrWhiteSpace(realFileName)) return false;
                    if (normalizedResponse.Contains(realFileName.ToLowerInvariant())) return true;
                    // Content-keyword fallback: if AI's response shares words with the chunk
                    // content, the chunk almost certainly grounded the answer.
                    if (!string.IsNullOrWhiteSpace(c.Content))
                    {
                        var contentLower = c.Content.ToLowerInvariant();
                        return responseWords.Any(word => contentLower.Contains(word));
                    }
                    return false;
                })
                // Keep only 1 citation per document:
                // prefer chunks with real content over TOC entries;
                // for exhaustive queries also rank by keyword overlap with the response.
                .GroupBy(c => c.DocumentId)
                .Select(g =>
                {
                    return g
                        .OrderByDescending(c => IsTocOrSummary(c) ? 0 : 1)
                        .ThenByDescending(c =>
                        {
                            if (!isExhaustive || string.IsNullOrWhiteSpace(c.Content)) return c.Relevance;
                            var contentLower = c.Content.ToLowerInvariant();
                            return responseWords.Count(word => contentLower.Contains(word));
                        })
                        .First();
                })
                .ToList();

            // Post-LLM guard: if AI produced a real answer but Step 3 stripped all citations,
            // surface that the answer cannot be grounded.
            if (isRelevant && !ragCitations.Any())
            {
                _logger.LogWarning(
                    "Post-LLM guard: AI returned IsRelevant=true but Step 3 removed all citations. Forcing no-answer.");
                aiResponse = "Tài liệu của bạn không chứa thông tin này hoặc không tìm thấy tài liệu.";
                isRelevant = false;
            }

            // Step 4: strip any "Nguồn" paragraph AI may have added — citations are already in the UI
            aiResponse = System.Text.RegularExpressions.Regex.Replace(
                aiResponse,
                @"[\n\r]*Nguồn.*",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
        }
        else
        {
            aiResponse = "Vui lòng đính kèm một tài liệu để tôi có thể trả lời câu hỏi của bạn dựa trên nội dung tài liệu.";
            isRelevant = false;
        }

        // Record token usage
        if (inputTokens > 0 || outputTokens > 0)
        {
            await _tokenTracker.RecordUsageAsync(userId, inputTokens, outputTokens, "chat", ct);
        }

        var assistantMessage = new ChatMessage
        {
            ChatSessionId = session.Id,
            Sender = "assistant",
            Content = aiResponse,
            IsRelevant = isRelevant,
            Citations = ragCitations
                .Select(c => new ChatMessageCitation
                {
                    CitationIndex = c.CitationIndex,
                    DocumentId = c.DocumentId,
                    Source = docIdToFileName.GetValueOrDefault(c.DocumentId, c.Source),
                    Snippet = c.Content,
                    PageNumber = c.PageNumber,
                    Relevance = c.Relevance,
                    MatchType = c.MatchType,
                    IsHighlightable = c.IsHighlightable,
                    Reason = c.Reason
                })
                .ToList()
        };

        await _unitOfWork.ChatMessages.AddAsync(assistantMessage, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return _mapper.Map<ChatMessageResponseDto>(assistantMessage);
    }

    public async Task<ChatSessionDocumentResponseDto> AddDocumentAsync(Guid sessionId, Guid documentId, Guid userId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            throw new KeyNotFoundException($"Chat session with ID {sessionId} not found.");
        }
        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not own this chat session.");
        }

        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found.");
        }
        if (document.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not own this document.");
        }

        var existing = await _unitOfWork.ChatSessionDocuments
            .Query()
            .FirstOrDefaultAsync(x => x.ChatSessionId == sessionId && x.DocumentId == documentId, ct);

        if (existing is not null)
        {
            return new ChatSessionDocumentResponseDto(
                existing.ChatSessionId,
                existing.DocumentId,
                document.Title,
                document.FileName,
                existing.CreatedAt);
        }

        var link = new ChatSessionDocument
        {
            Id = Guid.NewGuid(),
            ChatSessionId = sessionId,
            DocumentId = documentId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ChatSessionDocuments.AddAsync(link, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ChatSessionDocumentResponseDto(
            link.ChatSessionId,
            link.DocumentId,
            document.Title,
            document.FileName,
            link.CreatedAt);
    }

    public async Task RemoveDocumentAsync(Guid sessionId, Guid documentId, Guid userId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            throw new KeyNotFoundException($"Chat session with ID {sessionId} not found.");
        }
        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not own this chat session.");
        }

        var link = await _unitOfWork.ChatSessionDocuments
            .Query()
            .FirstOrDefaultAsync(x => x.ChatSessionId == sessionId && x.DocumentId == documentId, ct);

        if (link is null)
        {
            throw new KeyNotFoundException($"Document {documentId} is not attached to session {sessionId}.");
        }

        _unitOfWork.ChatSessionDocuments.Remove(link);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChatSessionDocumentResponseDto>> GetDocumentsAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            throw new KeyNotFoundException($"Chat session with ID {sessionId} not found.");
        }
        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not own this chat session.");
        }

        var links = await _unitOfWork.ChatSessionDocuments
            .Query()
            .Include(x => x.Document)
            .Where(x => x.ChatSessionId == sessionId)
            .AsNoTracking()
            .ToListAsync(ct);

        return links.Select(x => new ChatSessionDocumentResponseDto(
            x.ChatSessionId,
            x.DocumentId,
            x.Document.Title,
            x.Document.FileName,
            x.CreatedAt)).ToList();
    }
}
