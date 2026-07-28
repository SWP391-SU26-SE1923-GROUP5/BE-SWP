using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Tracking;
using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
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

    private const int EstimatedChatTokens = 1500;

    public AIChatService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IOpenAIService openAiService,
        ISemanticKernelOrchestrator orchestrator,
        ITokenTrackerService tokenTracker)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _openAIService = openAiService;
        _orchestrator = orchestrator;
        _tokenTracker = tokenTracker;
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
            await _unitOfWork.ChatSessions.AddAsync(session);
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            session = await _unitOfWork.ChatSessions.GetByIdAsync(request.SessionId.Value);
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
        await _unitOfWork.ChatMessages.AddAsync(userMessage);
        await _unitOfWork.SaveChangesAsync();

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
        IReadOnlyList<ChatCitationDto> citations = Array.Empty<ChatCitationDto>();

        if (docIds != null && docIds.Count > 0)
        {
            var ragResponse = await _orchestrator.AskWithTrackingAsync(userId, docIds, request.Message, history, ct);
            aiResponse = ragResponse.Answer;
            inputTokens = ragResponse.InputTokens;
            outputTokens = ragResponse.OutputTokens;
            isRelevant = ragResponse.IsRelevant;

            if (ragResponse.Citations is { Count: > 0 })
            {
                citations = ragResponse.Citations
                    .Select(c => new ChatCitationDto(
                        c.DocumentId,
                        c.Source,
                        c.Content.Length > 300 ? c.Content[..300] + "…" : c.Content,
                        c.PageNumber,
                        c.Relevance,
                        c.MatchType,
                        c.IsHighlightable,
                        c.Reason))
                    .ToList();
            }
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
            Citations = citations.Select((citation, index) =>
            {
                if (citation.DocumentId == Guid.Empty
                    || string.IsNullOrWhiteSpace(citation.Source)
                    || string.IsNullOrWhiteSpace(citation.Snippet))
                {
                    throw new InvalidOperationException("Citation snapshot is missing required source metadata.");
                }

                return new ChatMessageCitation
                {
                    CitationIndex = index + 1,
                    DocumentId = citation.DocumentId,
                    Source = citation.Source,
                    Snippet = citation.Snippet,
                    PageNumber = citation.PageNumber,
                    Relevance = citation.Relevance,
                    MatchType = citation.MatchType,
                    IsHighlightable = citation.IsHighlightable,
                    Reason = citation.Reason
                };
            }).ToList()
        };

        await _unitOfWork.ChatMessages.AddAsync(assistantMessage);
        await _unitOfWork.SaveChangesAsync(ct);

        var created = await _unitOfWork.ChatMessages
            .Query()
            .AsNoTracking()
            .FirstAsync(chatMessage => chatMessage.Id == assistantMessage.Id, ct);

        return new ChatMessageResponseDto(
            created.Id,
            created.ChatSessionId,
            created.Sender,
            created.Content,
            created.CreatedAt,
            created.UpdatedAt,
            isRelevant,
            citations);
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
