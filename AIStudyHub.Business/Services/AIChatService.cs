using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

public sealed class AIChatService : IAIChatService
{
    private const int MaxHistoryTurns = 20;

    private const string SystemPrompt = """
        You are a friendly, concise study assistant inside the AIStudyHub app.
        You help students learn by answering questions, explaining concepts,
        and summarizing material from the documents they have uploaded.

        Guidelines:
        - Reply in the same language the user uses.
        - Be brief unless the user asks for a deep explanation.
        - If you do not know the answer, say so honestly.
        - Do not invent facts; if the answer depends on a specific document,
          mention that you do not have access to that document.
        - Do not wrap your reply in JSON, markdown code fences, or "assistant:" prefixes.
          Just write the natural-language reply directly.
        """;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateChatSessionRequestDto> _createSessionValidator;
    private readonly IValidator<CreateChatMessageRequestDto> _createMessageValidator;
    private readonly ILocalAIService _localAIService;
    private readonly ILogger<AIChatService> _logger;

    public AIChatService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<CreateChatSessionRequestDto> createSessionValidator,
        IValidator<CreateChatMessageRequestDto> createMessageValidator,
        ILocalAIService localAIService,
        ILogger<AIChatService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _localAIService = localAIService;
        _createSessionValidator = createSessionValidator;
        _createMessageValidator = createMessageValidator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync(Guid? userId = null)
    {
        var query = _unitOfWork.ChatSessions.Query();

        if (userId.HasValue)
        {
            query = query.Where(s => s.UserId == userId.Value);
        }

        var sessions = await query
            .Include(session => session.User)
            .Include(session => session.Document)
            .AsNoTracking()
            .OrderByDescending(session => session.CreatedAt)
            .ToListAsync();

        return sessions.Select(_mapper.Map<ChatSessionResponseDto>).ToList();
    }

    public async Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request, Guid userId)
    {
        await _createSessionValidator.ValidateAndThrowAsync(request);

        var document = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document with ID {request.DocumentId} not found.");
        }

        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentId = request.DocumentId,
            SessionTitle = request.SessionTitle ?? "New Chat",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ChatSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync();

        var created = await _unitOfWork.ChatSessions
            .Query()
            .Include(chatSession => chatSession.User)
            .Include(chatSession => chatSession.Document)
            .AsNoTracking()
            .FirstAsync(chatSession => chatSession.Id == session.Id);

        return _mapper.Map<ChatSessionResponseDto>(created);
    }

    public async Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId, Guid userId)
    {
        var session = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId);
        if (session is null)
        {
            throw new KeyNotFoundException($"Chat session with ID {sessionId} not found.");
        }

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have access to this chat session.");
        }

        var messages = await _unitOfWork.ChatMessages
            .Query()
            .Where(message => message.ChatSessionId == sessionId)
            .OrderBy(message => message.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return messages.Select(_mapper.Map<ChatMessageResponseDto>).ToList();
    }

    public async Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request, Guid userId)
    {
        await _createMessageValidator.ValidateAndThrowAsync(request);

        var session = await _unitOfWork.ChatSessions.GetByIdAsync(request.SessionId);
        if (session is null)
        {
            throw new KeyNotFoundException($"Chat session with ID {request.SessionId} not found.");
        }

        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = request.SessionId,
            Sender = "user",
            Content = request.Message,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ChatMessages.AddAsync(userMessage);
        await _unitOfWork.SaveChangesAsync();

        var history = await LoadHistoryAsync(request.SessionId, excludeMessageId: userMessage.Id);
        var aiReply = await GetAssistantReplyAsync(history, request.Message);

        var assistantMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = request.SessionId,
            Sender = "assistant",
            Content = aiReply,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ChatMessages.AddAsync(assistantMessage);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<ChatMessageResponseDto>(assistantMessage);
    }

    private async Task<List<ChatTurn>> LoadHistoryAsync(Guid sessionId, Guid excludeMessageId)
    {
        var recent = await _unitOfWork.ChatMessages
            .Query()
            .Where(message => message.ChatSessionId == sessionId && message.Id != excludeMessageId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(MaxHistoryTurns)
            .AsNoTracking()
            .ToListAsync();

        return recent
            .OrderBy(message => message.CreatedAt)
            .Select(message => new ChatTurn(
                string.Equals(message.Sender, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                message.Content))
            .ToList();
    }

    private async Task<string> GetAssistantReplyAsync(
        IReadOnlyList<ChatTurn> history,
        string userMessage)
    {
        try
        {
            var reply = await _localAIService.SendChatAsync(
                SystemPrompt,
                history,
                userMessage);

            return string.IsNullOrWhiteSpace(reply)
                ? "I am sorry, I could not generate a response right now. Please try again."
                : reply.Trim();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "LLM server connection failed. URL: {Url}", "(see RagOptions)");
            return "I could not reach the AI server. Please make sure the local AI server is running and try again.";
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "LLM request timed out");
            return "The AI took too long to respond. Please try a shorter message.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling LLM for chat");
            return "I am having trouble generating a response right now. Please try again in a moment.";
        }
    }
}
