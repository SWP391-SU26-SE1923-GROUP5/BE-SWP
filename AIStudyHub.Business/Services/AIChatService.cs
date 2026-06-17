using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed class AIChatService : IAIChatService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateChatSessionRequestDto> _createSessionValidator;
    private readonly IValidator<CreateChatMessageRequestDto> _createMessageValidator;
    private readonly ILocalAIService _localAIService;
    public AIChatService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<CreateChatSessionRequestDto> createSessionValidator,
        IValidator<CreateChatMessageRequestDto> createMessageValidator,
        ILocalAIService openAiService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _localAIService = openAiService;
        _createSessionValidator = createSessionValidator;
        _createMessageValidator = createMessageValidator;
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

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have access to this chat session.");
        }

        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = request.SessionId,
            Content = request.Message,
            Sender = "user",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ChatMessages.AddAsync(userMessage);
        await _unitOfWork.SaveChangesAsync();

        var aiResponse = await _localAIService.SendMessageAsync(request.Message);

        var assistantMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = request.SessionId,
            Sender = "assistant",
            Content = aiResponse,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ChatMessages.AddAsync(assistantMessage);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<ChatMessageResponseDto>(assistantMessage);
    }
}
