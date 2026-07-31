using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Tracking;
using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Services;
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
            .OrderBy(message => message.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return messages.Select(_mapper.Map<ChatMessageResponseDto>).ToList();
    }

    public async Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request, Guid userId, CancellationToken ct = default)
    {
        ChatSession? session = null;
        List<ChatSessionDocument> sessionDocumentLinks = [];
        if (request.SessionId.HasValue)
        {
            session = await _unitOfWork.ChatSessions.GetByIdAsync(request.SessionId.Value, ct);
            if (session is null || session.UserId != userId)
            {
                throw new KeyNotFoundException($"Chat session with ID {request.SessionId} not found or access denied.");
            }

            sessionDocumentLinks = await _unitOfWork.ChatSessionDocuments
                .Query()
                .Include(link => link.Document)
                .Where(link => link.ChatSessionId == session.Id)
                .AsNoTracking()
                .ToListAsync(ct);

            var blockers = sessionDocumentLinks
                .Select(link => new
                {
                    Link = link,
                    Readiness = DocumentReadinessEvaluator.Evaluate(link.Document)
                })
                .Where(item => !item.Readiness.IsChatReady)
                .Select(item => new BlockingDocumentResponseDto(
                    item.Link.DocumentId,
                    item.Link.Document.Title,
                    item.Readiness.Status,
                    item.Readiness.IsChatReady,
                    item.Readiness.Message,
                    item.Readiness.CanRetry))
                .ToList();

            if (blockers.Count > 0)
            {
                throw new DocumentsNotReadyException(blockers);
            }
        }

        // Check AI token quota only after validating every existing attachment.
        if (!await _tokenTracker.HasQuotaAsync(userId, EstimatedChatTokens, ct))
        {
            var (current, limit) = await _tokenTracker.GetUsageInfoAsync(userId, ct);
            throw new QuotaExceededException(current, limit, EstimatedChatTokens);
        }

        if (!request.SessionId.HasValue)
        {
            // New sessions have no attachments and retain the existing prompt to attach one.
            var title = request.Message.Length > 50 ? request.Message.Substring(0, 47) + "..." : request.Message;
            session = new ChatSession
            {
                UserId = userId,
                SessionTitle = title
            };
            await _unitOfWork.ChatSessions.AddAsync(session, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var activeSession = session!;
        var userMessage = new ChatMessage
        {
            ChatSessionId = activeSession.Id,
            Sender = "user",
            Content = request.Message
        };
        await _unitOfWork.ChatMessages.AddAsync(userMessage, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var history = await _unitOfWork.ChatMessages
            .Query()
            .Where(m => m.ChatSessionId == activeSession.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
        history.Reverse();

        // Use all documents attached to the session
        IReadOnlyList<Guid>? docIds = sessionDocumentLinks.Count > 0
            ? sessionDocumentLinks.Select(link => link.DocumentId).ToList()
            : null;

        string aiResponse;
        int inputTokens = 0;
        int outputTokens = 0;
        bool isRelevant = false;

        if (docIds != null && docIds.Count > 0)
        {
            var ragResponse = await _orchestrator.AskWithTrackingAsync(userId, docIds, request.Message, history, ct);
            aiResponse = ragResponse.Answer;
            inputTokens = ragResponse.InputTokens;
            outputTokens = ragResponse.OutputTokens;
            isRelevant = ragResponse.IsRelevant;
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
            ChatSessionId = activeSession.Id,
            Sender = "assistant",
            Content = aiResponse,
            IsRelevant = isRelevant
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
            return MapSessionDocument(existing, document);
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

        return MapSessionDocument(link, document);
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

        return links.Select(link => MapSessionDocument(link, link.Document)).ToList();
    }

    private static ChatSessionDocumentResponseDto MapSessionDocument(
        ChatSessionDocument link,
        Document document)
    {
        var readiness = DocumentReadinessEvaluator.Evaluate(document);
        return new ChatSessionDocumentResponseDto(
            link.ChatSessionId,
            link.DocumentId,
            document.Title,
            document.FileName,
            link.CreatedAt,
            readiness.Status,
            readiness.IsChatReady,
            readiness.Message,
            readiness.CanRetry);
    }
}
