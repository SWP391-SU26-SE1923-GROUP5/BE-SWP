using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed class AIChatService : IAIChatService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateChatSessionRequestDto> _createSessionValidator;
    private readonly IValidator<CreateChatMessageRequestDto> _createMessageValidator;

    public AIChatService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<CreateChatSessionRequestDto> createSessionValidator,
        IValidator<CreateChatMessageRequestDto> createMessageValidator)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
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

        var message = _mapper.Map<ChatMessage>(request);
        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        var created = await _unitOfWork.ChatMessages
            .Query()
            .AsNoTracking()
            .FirstAsync(chatMessage => chatMessage.Id == message.Id);

        return _mapper.Map<ChatMessageResponseDto>(created);
    }
}
