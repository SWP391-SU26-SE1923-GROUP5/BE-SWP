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

    public async Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync()
    {
        var sessions = await _unitOfWork.ChatSessions
            .Query()
            .Include(session => session.User)
            .Include(session => session.Document)
            .AsNoTracking()
            .OrderByDescending(session => session.CreatedAt)
            .ToListAsync();

        return sessions.Select(_mapper.Map<ChatSessionResponseDto>).ToList();
    }

    public async Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request)
    {
        await _createSessionValidator.ValidateAndThrowAsync(request);

        var documentExists = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId) is not null;
        if (!documentExists)
        {
            throw new KeyNotFoundException($"Document with ID {request.DocumentId} not found.");
        }

        var session = _mapper.Map<ChatSession>(request);
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

    public async Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId)
    {
        var sessionExists = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId) is not null;
        if (!sessionExists)
        {
            throw new KeyNotFoundException($"Chat session with ID {sessionId} not found.");
        }

        var messages = await _unitOfWork.ChatMessages
            .Query()
            .Where(message => message.ChatSessionId == sessionId)
            .OrderBy(message => message.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return messages.Select(_mapper.Map<ChatMessageResponseDto>).ToList();
    }

    public async Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request)
    {
        await _createMessageValidator.ValidateAndThrowAsync(request);

        var sessionExists = await _unitOfWork.ChatSessions.GetByIdAsync(request.SessionId) is not null;
        if (!sessionExists)
        {
            throw new KeyNotFoundException($"Chat session with ID {request.SessionId} not found.");
        }

        // Save user message
        var userMessage = _mapper.Map<ChatMessage>(request);
        userMessage.Sender = userMessage.Sender ?? "user";
        await _unitOfWork.ChatMessages.AddAsync(userMessage);
        await _unitOfWork.SaveChangesAsync();

        // Call OpenAI to get assistant response
        var aiResponse = await _localAIService.SendMessageAsync(request.Message);

        // Persist assistant message
        var assistantMessage = new ChatMessage
        {
            ChatSessionId = request.SessionId,
            Sender = "assistant",
            Content = aiResponse
        };

        await _unitOfWork.ChatMessages.AddAsync(assistantMessage);
        await _unitOfWork.SaveChangesAsync();

        var created = await _unitOfWork.ChatMessages
            .Query()
            .AsNoTracking()
            .FirstAsync(chatMessage => chatMessage.Id == assistantMessage.Id);

        return _mapper.Map<ChatMessageResponseDto>(created);
    }
}
