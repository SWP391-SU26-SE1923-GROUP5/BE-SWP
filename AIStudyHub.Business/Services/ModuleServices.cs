using Microsoft.AspNetCore.Http;
using AIStudyHub.Business.DTOs.Answers;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.DTOs.Payments;
using AIStudyHub.Business.DTOs.Questions;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.DTOs.QuizSubmissions;
using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Business.DTOs.Subjects;
using AIStudyHub.Business.DTOs.TierMemberships;
using AIStudyHub.Business.DTOs.Votes;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AIStudyHub.Business.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public DocumentService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<DocumentResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Include(d => d.Votes)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return documents.Select(d => new DocumentResponseDto(
            d.Id,
            d.UserId,
            d.SubjectId,
            d.Title,
            d.FileLink,
            d.FileName,
            d.FileExtension,
            d.FileType,
            d.SharedUsers,
            d.ShareStatus,
            d.Status,
            d.Votes.Count,
            d.CreatedAt,
            d.UpdatedAt)).ToList();
    }

    public async Task<IReadOnlyList<DocumentResponseDto>> GetAllByUserIdAsync(Guid userId, string? keyword = null, Guid? subjectId = null, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Documents
            .Query()
            .Where(d => d.UserId == userId || d.ShareStatus == "public");

        if (subjectId.HasValue)
        {
            query = query.Where(d => d.SubjectId == subjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.ToLower();
            query = query.Where(d => d.Title.ToLower().Contains(lowerKeyword) || (d.FileName != null && d.FileName.ToLower().Contains(lowerKeyword)));
        }

        var documents = await query
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Include(d => d.Votes)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return documents.Select(d => new DocumentResponseDto(
            d.Id,
            d.UserId,
            d.SubjectId,
            d.Title,
            d.FileLink,
            d.FileName,
            d.FileExtension,
            d.FileType,
            d.SharedUsers,
            d.ShareStatus,
            d.Status,
            d.Votes.Count,
            d.CreatedAt,
            d.UpdatedAt)).ToList();
    }

    public async Task<DocumentResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Include(d => d.Votes)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document is null) return null;

        return new DocumentResponseDto(
            document.Id,
            document.UserId,
            document.SubjectId,
            document.Title,
            document.FileLink,
            document.FileName,
            document.FileExtension,
            document.FileType,
            document.SharedUsers,
            document.ShareStatus,
            document.Status,
            document.Votes.Count,
            document.CreatedAt,
            document.UpdatedAt);
    }

    public async Task<DocumentResponseDto> CreateAsync(CreateDocumentRequestDto request, CancellationToken cancellationToken = default)
    {
        var subjectExists = await _unitOfWork.Subjects.GetByIdAsync(request.SubjectId, cancellationToken) is not null;
        if (!subjectExists)
        {
            throw new InvalidOperationException($"Subject with ID {request.SubjectId} not found.");
        }

        var document = _mapper.Map<Data.Entities.Document>(request);
        await _unitOfWork.Documents.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == document.Id, cancellationToken);

        return _mapper.Map<DocumentResponseDto>(created);
    }

    public async Task<DocumentResponseDto> UpdateAsync(Guid id, UpdateDocumentRequestDto request, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document with ID {id} not found.");
        }

        _mapper.Map(request, document);
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return _mapper.Map<DocumentResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document with ID {id} not found.");
        }

        _unitOfWork.Documents.Remove(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShareDocumentResponseDto> ShareDocumentAsync(
        Guid documentId,
        Guid callerId,
        ShareDocumentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        if (document.UserId != callerId)
        {
            throw new UnauthorizedAccessException("Only the document owner can change its sharing settings.");
        }

        // De-duplicate and drop the caller (a user cannot share a document with themselves).
        var sharedUserIds = request.SharedUserIds?
            .Where(id => id != Guid.Empty && id != callerId)
            .Distinct()
            .ToList() ?? new List<Guid>();

        // Validate that every id actually maps to an existing active user.
        if (sharedUserIds.Count > 0)
        {
            var existingIds = await _unitOfWork.Users
                .Query()
                .Where(u => sharedUserIds.Contains(u.Id) && u.IsActive && u.Status == "active")
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            sharedUserIds = existingIds;
        }

        // Persist as a JSON array string to keep the column format consistent with other usages.
        document.SharedUsers = sharedUserIds.Count == 0
            ? null
            : System.Text.Json.JsonSerializer.Serialize(sharedUserIds);

        // Derive the share status from the resulting list. This endpoint does NOT
        // accept an explicit status from the caller — status is owned by PUT /api/Document/{id}.
        document.ShareStatus = sharedUserIds.Count > 0 ? "shared" : "private";

        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ShareDocumentResponseDto(document.Id, sharedUserIds);
    }
}

public sealed class VoteService : IVoteService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public VoteService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<VoteResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var votes = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return votes.Select(_mapper.Map<VoteResponseDto>).ToList();
    }

    public async Task<VoteResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vote = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        return vote is null ? null : _mapper.Map<VoteResponseDto>(vote);
    }

    public async Task<VoteResponseDto> CreateVoteAsync(Guid userId, Guid documentId, VoteType type, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Votes
            .Query()
            .FirstOrDefaultAsync(v => v.UserId == userId && v.DocumentId == documentId, cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException("User has already voted on this document.");
        }

        var documentExists = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken) is not null;
        if (!documentExists)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found.");
        }

        var vote = new Data.Entities.Vote
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentId = documentId,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Votes.AddAsync(vote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vote.Id, cancellationToken);

        return _mapper.Map<VoteResponseDto>(created);
    }

    public async Task<VoteResponseDto> CreateAsync(CreateVoteRequestDto request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use CreateVoteAsync with explicit userId for security.");
    }

    public async Task<VoteResponseDto> UpdateAsync(Guid id, UpdateVoteRequestDto request, CancellationToken cancellationToken = default)
    {
        var vote = await _unitOfWork.Votes.GetByIdAsync(id, cancellationToken);
        if (vote is null)
        {
            throw new KeyNotFoundException($"Vote with ID {id} not found.");
        }

        vote.Type = request.Type;
        vote.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Votes.Update(vote);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        return _mapper.Map<VoteResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vote = await _unitOfWork.Votes.GetByIdAsync(id, cancellationToken);
        if (vote is null)
        {
            throw new KeyNotFoundException($"Vote with ID {id} not found.");
        }

        _unitOfWork.Votes.Remove(vote);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ReportService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ReportResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var reports = await _unitOfWork.Reports
            .Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reports.Select(_mapper.Map<ReportResponseDto>).ToList();
    }

    public async Task<ReportResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await _unitOfWork.Reports
            .Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return report is null ? null : _mapper.Map<ReportResponseDto>(report);
    }

    public async Task<ReportResponseDto> CreateAsync(CreateReportRequestDto request, CancellationToken cancellationToken = default)
    {
        var documentExists = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId, cancellationToken) is not null;
        if (!documentExists)
        {
            throw new KeyNotFoundException($"Document with ID {request.DocumentId} not found.");
        }

        var report = _mapper.Map<Data.Entities.Report>(request);
        await _unitOfWork.Reports.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Reports
            .Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == report.Id, cancellationToken);

        return _mapper.Map<ReportResponseDto>(created);
    }

    public async Task<ReportResponseDto> UpdateAsync(Guid id, UpdateReportRequestDto request, CancellationToken cancellationToken = default)
    {
        var report = await _unitOfWork.Reports.GetByIdAsync(id, cancellationToken);
        if (report is null)
        {
            throw new KeyNotFoundException($"Report with ID {id} not found.");
        }

        _mapper.Map(request, report);
        _unitOfWork.Reports.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Reports
            .Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return _mapper.Map<ReportResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await _unitOfWork.Reports.GetByIdAsync(id, cancellationToken);
        if (report is null)
        {
            throw new KeyNotFoundException($"Report with ID {id} not found.");
        }

        _unitOfWork.Reports.Remove(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class FlashcardService : IFlashcardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public FlashcardService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<FlashcardResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var flashcards = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return flashcards.Select(_mapper.Map<FlashcardResponseDto>).ToList();
    }

    public async Task<FlashcardResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var flashcard = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        return flashcard is null ? null : _mapper.Map<FlashcardResponseDto>(flashcard);
    }

    public async Task<FlashcardResponseDto> CreateAsync(CreateFlashcardRequestDto request, CancellationToken cancellationToken = default)
    {
        var documentExists = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId, cancellationToken) is not null;
        if (!documentExists)
        {
            throw new KeyNotFoundException($"Document with ID {request.DocumentId} not found.");
        }

        var flashcard = _mapper.Map<Data.Entities.Flashcard>(request);
        await _unitOfWork.Flashcards.AddAsync(flashcard, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == flashcard.Id, cancellationToken);

        return _mapper.Map<FlashcardResponseDto>(created);
    }

    public async Task<FlashcardResponseDto> UpdateAsync(Guid id, UpdateFlashcardRequestDto request, CancellationToken cancellationToken = default)
    {
        var flashcard = await _unitOfWork.Flashcards.GetByIdAsync(id, cancellationToken);
        if (flashcard is null)
        {
            throw new KeyNotFoundException($"Flashcard with ID {id} not found.");
        }

        _mapper.Map(request, flashcard);
        _unitOfWork.Flashcards.Update(flashcard);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        return _mapper.Map<FlashcardResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var flashcard = await _unitOfWork.Flashcards.GetByIdAsync(id, cancellationToken);
        if (flashcard is null)
        {
            throw new KeyNotFoundException($"Flashcard with ID {id} not found.");
        }

        _unitOfWork.Flashcards.Remove(flashcard);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FlashcardResponseDto>> GetByDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var flashcards = await _unitOfWork.Flashcards
            .Query()
            .Where(f => f.DocumentId == documentId)
            .OrderBy(f => f.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return flashcards.Select(_mapper.Map<FlashcardResponseDto>).ToList();
    }

    public async Task<IReadOnlyList<FlashcardResponseDto>> SaveGeneratedBatchAsync(
        SaveGeneratedFlashcardsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Flashcards is null || request.Flashcards.Count == 0)
            return Array.Empty<FlashcardResponseDto>();

        var documentExists = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId, cancellationToken) is not null;
        if (!documentExists)
        {
            throw new KeyNotFoundException($"Document with ID {request.DocumentId} not found.");
        }

        var existingFronts = (await _unitOfWork.Flashcards
            .Query()
            .Where(f => f.DocumentId == request.DocumentId)
            .Select(f => f.Front)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<Data.Entities.Flashcard>();
        foreach (var card in request.Flashcards)
        {
            if (string.IsNullOrWhiteSpace(card.Front) || string.IsNullOrWhiteSpace(card.Back))
                continue;

            var cleanFront = card.Front.Trim();
            var cleanBack = card.Back.Trim();

            if (!existingFronts.Add(cleanFront))
                continue;

            created.Add(new Data.Entities.Flashcard
            {
                DocumentId = request.DocumentId,
                Front = cleanFront,
                Back = cleanBack
            });
        }

        if (created.Count == 0)
            return Array.Empty<FlashcardResponseDto>();

        foreach (var card in created)
            await _unitOfWork.Flashcards.AddAsync(card, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var ids = created.Select(c => c.Id).ToList();
        var saved = await _unitOfWork.Flashcards
            .Query()
            .Where(f => ids.Contains(f.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return saved.Select(_mapper.Map<FlashcardResponseDto>).ToList();
    }

    public async Task<IReadOnlyList<FlashcardResponseDto>> CreateBulkAsync(
        IReadOnlyList<CreateFlashcardRequestDto> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests is null || requests.Count == 0)
            return Array.Empty<FlashcardResponseDto>();

        var documentIds = requests.Select(r => r.DocumentId).Distinct().ToList();
        var allDocumentsExist = await _unitOfWork.Documents
            .Query()
            .Where(d => documentIds.Contains(d.Id))
            .Select(d => d.Id)
            .CountAsync(cancellationToken) == documentIds.Count;

        if (!allDocumentsExist)
            throw new KeyNotFoundException("One or more documents not found.");

        var flashcards = requests
            .Select(r => _mapper.Map<Data.Entities.Flashcard>(r))
            .ToList();

        foreach (var flashcard in flashcards)
            await _unitOfWork.Flashcards.AddAsync(flashcard, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var ids = flashcards.Select(f => f.Id).ToList();
        var saved = await _unitOfWork.Flashcards
            .Query()
            .Where(f => ids.Contains(f.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return saved.Select(_mapper.Map<FlashcardResponseDto>).ToList();
    }
}

public sealed class QuizService : IQuizService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public QuizService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<QuizResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var quizzes = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return quizzes.Select(_mapper.Map<QuizResponseDto>).ToList();
    }

    public async Task<QuizResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quiz = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return quiz is null ? null : _mapper.Map<QuizResponseDto>(quiz);
    }

    public async Task<QuizResponseDto> CreateAsync(CreateQuizRequestDto request, CancellationToken cancellationToken = default)
    {
        var documentExists = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId, cancellationToken) is not null;
        if (!documentExists)
        {
            throw new KeyNotFoundException($"Document with ID {request.DocumentId} not found.");
        }

        var quiz = _mapper.Map<Data.Entities.Quiz>(request);
        await _unitOfWork.Quizzes.AddAsync(quiz, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quiz.Id, cancellationToken);

        return _mapper.Map<QuizResponseDto>(created);
    }

    public async Task<QuizResponseDto> UpdateAsync(Guid id, UpdateQuizRequestDto request, CancellationToken cancellationToken = default)
    {
        var quiz = await _unitOfWork.Quizzes.GetByIdAsync(id, cancellationToken);
        if (quiz is null)
        {
            throw new KeyNotFoundException($"Quiz with ID {id} not found.");
        }

        _mapper.Map(request, quiz);
        _unitOfWork.Quizzes.Update(quiz);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return _mapper.Map<QuizResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quiz = await _unitOfWork.Quizzes.GetByIdAsync(id, cancellationToken);
        if (quiz is null)
        {
            throw new KeyNotFoundException($"Quiz with ID {id} not found.");
        }

        _unitOfWork.Quizzes.Remove(quiz);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class QuestionService : IQuestionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICitationService _citationService;

    public QuestionService(IUnitOfWork unitOfWork, IMapper mapper, ICitationService citationService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _citationService = citationService;
    }

    public async Task<IReadOnlyList<QuestionResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var questions = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return questions.Select(_mapper.Map<QuestionResponseDto>).ToList();
    }

    public async Task<QuestionResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var question = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return question is null ? null : _mapper.Map<QuestionResponseDto>(question);
    }

    public async Task<QuestionResponseDto> CreateAsync(CreateQuestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var quizExists = await _unitOfWork.Quizzes.GetByIdAsync(request.QuizId, cancellationToken) is not null;
        if (!quizExists)
        {
            throw new KeyNotFoundException($"Quiz with ID {request.QuizId} not found.");
        }

        var question = _mapper.Map<Data.Entities.Question>(request);
        await _unitOfWork.Questions.AddAsync(question, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == question.Id, cancellationToken);

        return _mapper.Map<QuestionResponseDto>(created);
    }

    public async Task<QuestionResponseDto> UpdateAsync(Guid id, UpdateQuestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var question = await _unitOfWork.Questions.GetByIdAsync(id, cancellationToken);
        if (question is null)
        {
            throw new KeyNotFoundException($"Question with ID {id} not found.");
        }

        _mapper.Map(request, question);
        _unitOfWork.Questions.Update(question);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return _mapper.Map<QuestionResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var question = await _unitOfWork.Questions.GetByIdAsync(id, cancellationToken);
        if (question is null)
        {
            throw new KeyNotFoundException($"Question with ID {id} not found.");
        }

        _unitOfWork.Questions.Remove(question);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AnswerService : IAnswerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public AnswerService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AnswerResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var answers = await _unitOfWork.Answers
            .Query()
            .Include(a => a.Question)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return answers.Select(_mapper.Map<AnswerResponseDto>).ToList();
    }

    public async Task<AnswerResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var answer = await _unitOfWork.Answers
            .Query()
            .Include(a => a.Question)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return answer is null ? null : _mapper.Map<AnswerResponseDto>(answer);
    }

    public async Task<AnswerResponseDto> CreateAsync(CreateAnswerRequestDto request, CancellationToken cancellationToken = default)
    {
        var questionExists = await _unitOfWork.Questions.GetByIdAsync(request.QuestionId, cancellationToken) is not null;
        if (!questionExists)
        {
            throw new KeyNotFoundException($"Question with ID {request.QuestionId} not found.");
        }

        var answer = _mapper.Map<Data.Entities.Answer>(request);
        await _unitOfWork.Answers.AddAsync(answer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Answers
            .Query()
            .Include(a => a.Question)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == answer.Id, cancellationToken);

        return _mapper.Map<AnswerResponseDto>(created);
    }

    public async Task<AnswerResponseDto> UpdateAsync(Guid id, UpdateAnswerRequestDto request, CancellationToken cancellationToken = default)
    {
        var answer = await _unitOfWork.Answers.GetByIdAsync(id, cancellationToken);
        if (answer is null)
        {
            throw new KeyNotFoundException($"Answer with ID {id} not found.");
        }

        _mapper.Map(request, answer);
        _unitOfWork.Answers.Update(answer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Answers
            .Query()
            .Include(a => a.Question)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return _mapper.Map<AnswerResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var answer = await _unitOfWork.Answers.GetByIdAsync(id, cancellationToken);
        if (answer is null)
        {
            throw new KeyNotFoundException($"Answer with ID {id} not found.");
        }

        _unitOfWork.Answers.Remove(answer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class QuizSubmissionService : IQuizSubmissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public QuizSubmissionService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<QuizSubmissionResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var submissions = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.User)
            .Include(qs => qs.Quiz)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return submissions.Select(_mapper.Map<QuizSubmissionResponseDto>).ToList();
    }

    public async Task<QuizSubmissionResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var submission = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.User)
            .Include(qs => qs.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(qs => qs.Id == id, cancellationToken);

        return submission is null ? null : _mapper.Map<QuizSubmissionResponseDto>(submission);
    }

    public async Task<QuizSubmissionResponseDto> CreateAsync(CreateQuizSubmissionRequestDto request, CancellationToken cancellationToken = default)
    {
        var quiz = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Questions)
                .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == request.QuizId, cancellationToken);

        if (quiz is null)
        {
            throw new KeyNotFoundException($"Quiz with ID {request.QuizId} not found.");
        }

        var submission = new Data.Entities.QuizSubmission
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            QuizId = request.QuizId,
            Answers = request.Answers,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        // Grade the submission
        if (quiz.Questions.Any())
        {
            var maxScore = quiz.Questions.Count;
            var totalCorrect = 0;

            // Simple grading: parse submitted answers and match with questions
            // Assuming request.Answers is JSON string like "{\"q1\":\"A\",\"q2\":\"B\"}"
            // And Question has Answers where IsCorrect == true
            var submittedAnswers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(request.Answers)
                ?? new Dictionary<string, string>();

            foreach (var question in quiz.Questions)
            {
                var correctAnswer = question.Answers.FirstOrDefault(a => a.IsCorrect);
                if (correctAnswer != null && submittedAnswers.TryGetValue(question.Id.ToString(), out var selectedOption))
                {
                    if (correctAnswer.SelectedOption.Equals(selectedOption, StringComparison.OrdinalIgnoreCase))
                    {
                        totalCorrect++;
                    }
                }
            }

            submission.Score = totalCorrect;
            submission.MaxScore = maxScore;
            submission.TotalCorrect = totalCorrect;
            submission.GradedAt = DateTime.UtcNow;
        }

        await _unitOfWork.QuizSubmissions.AddAsync(submission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.User)
            .Include(qs => qs.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(qs => qs.Id == submission.Id, cancellationToken);

        return _mapper.Map<QuizSubmissionResponseDto>(created);
    }

    public async Task<QuizSubmissionResponseDto> UpdateAsync(Guid id, UpdateQuizSubmissionRequestDto request, CancellationToken cancellationToken = default)
    {
        var submission = await _unitOfWork.QuizSubmissions.GetByIdAsync(id, cancellationToken);
        if (submission is null)
        {
            throw new KeyNotFoundException($"Quiz submission with ID {id} not found.");
        }

        _mapper.Map(request, submission);
        _unitOfWork.QuizSubmissions.Update(submission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.User)
            .Include(qs => qs.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(qs => qs.Id == id, cancellationToken);

        return _mapper.Map<QuizSubmissionResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var submission = await _unitOfWork.QuizSubmissions.GetByIdAsync(id, cancellationToken);
        if (submission is null)
        {
            throw new KeyNotFoundException($"Quiz submission with ID {id} not found.");
        }

        _unitOfWork.QuizSubmissions.Remove(submission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public NotificationService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<NotificationResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await _unitOfWork.Notifications
            .Query()
            .Include(n => n.User)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return notifications.Select(_mapper.Map<NotificationResponseDto>).ToList();
    }

    public async Task<NotificationResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications
            .Query()
            .Include(n => n.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        return notification is null ? null : _mapper.Map<NotificationResponseDto>(notification);
    }

    public async Task<NotificationResponseDto> CreateAsync(CreateNotificationRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<Data.Enums.NotificationType>(request.Type, true, out var notificationType))
        {
            notificationType = Data.Enums.NotificationType.System;
        }

        var notification = new Data.Entities.Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Message = request.Message,
            Type = notificationType,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Notifications
            .Query()
            .Include(n => n.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notification.Id, cancellationToken);

        return new NotificationResponseDto(
            created!.Id,
            created.UserId,
            created.Message,
            created.IsRead,
            created.Type.ToString(),
            created.CreatedAt,
            created.UpdatedAt);
    }

    public async Task<NotificationResponseDto> UpdateAsync(Guid id, UpdateNotificationRequestDto request, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            throw new KeyNotFoundException($"Notification with ID {id} not found.");
        }

        _mapper.Map(request, notification);
        _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Notifications
            .Query()
            .Include(n => n.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        return _mapper.Map<NotificationResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            throw new KeyNotFoundException($"Notification with ID {id} not found.");
        }

        _unitOfWork.Notifications.Remove(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationResponseDto>> GetUserNotificationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _unitOfWork.Notifications
            .Query()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return notifications.Select(n => new NotificationResponseDto(
            n.Id, n.UserId, n.Message, n.IsRead, n.Type.ToString(), n.CreatedAt, n.UpdatedAt)).ToList();
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            throw new KeyNotFoundException($"Notification with ID {notificationId} not found.");
        }

        notification.IsRead = true;
        _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _unitOfWork.Notifications
            .Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            _unitOfWork.Notifications.Update(notification);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IVnPayService _vnPayService;

    public PaymentService(IUnitOfWork unitOfWork, IMapper mapper, IVnPayService vnPayService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _vnPayService = vnPayService;
    }

    public async Task<IReadOnlyList<PaymentResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var payments = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return payments.Select(_mapper.Map<PaymentResponseDto>).ToList();
    }

    public async Task<PaymentResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return payment is null ? null : _mapper.Map<PaymentResponseDto>(payment);
    }

    public async Task<PaymentResponseDto> CreateAsync(CreatePaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.TierId.HasValue)
        {
            var tier = await _unitOfWork.TierMemberships.GetByIdAsync(request.TierId.Value, cancellationToken);
            if (tier is null)
            {
                throw new KeyNotFoundException($"Tier membership with ID {request.TierId} not found.");
            }

            var payment = _mapper.Map<Data.Entities.Payment>(request);
            await _unitOfWork.Payments.AddAsync(payment, cancellationToken);

            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            if (user is not null)
            {
                user.TierId = request.TierId.Value;
                if (!tier.TierName.Equals("Free", StringComparison.OrdinalIgnoreCase))
                {
                    user.TierExpireAt = DateTime.UtcNow.AddDays(30);
                }
                else
                {
                    user.TierExpireAt = null;
                }
                _unitOfWork.Users.Update(user);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var created = await _unitOfWork.Payments
                .Query()
                .Include(p => p.User)
                .Include(p => p.TierMembership)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == payment.Id, cancellationToken);

            return _mapper.Map<PaymentResponseDto>(created);
        }

        var paymentNoTier = _mapper.Map<Data.Entities.Payment>(request);
        await _unitOfWork.Payments.AddAsync(paymentNoTier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var createdNoTier = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentNoTier.Id, cancellationToken);

        return _mapper.Map<PaymentResponseDto>(createdNoTier);
    }

    public async Task<PaymentResponseDto> UpdateAsync(Guid id, UpdatePaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(id, cancellationToken);
        if (payment is null)
        {
            throw new KeyNotFoundException($"Payment with ID {id} not found.");
        }

        _mapper.Map(request, payment);
        _unitOfWork.Payments.Update(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return _mapper.Map<PaymentResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(id, cancellationToken);
        if (payment is null)
        {
            throw new KeyNotFoundException($"Payment with ID {id} not found.");
        }

        _unitOfWork.Payments.Remove(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaymentLinkResponseDto> CreatePaymentUrlAsync(CreatePaymentLinkRequestDto request, HttpContext context, CancellationToken cancellationToken = default)
    {
        var userIdString = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            throw new UnauthorizedAccessException("User not authenticated or invalid ID.");
        }

        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(request.TierId, cancellationToken);
        if (tier is null)
        {
            throw new KeyNotFoundException($"Tier with ID {request.TierId} not found.");
        }

        var amount = 100000m; // Default amount for premium tier if not specified in tier entity. Assuming 100k VND
        var payment = new Data.Entities.Payment
        {
            UserId = userId,
            TierId = request.TierId,
            Amount = amount,
            Status = Data.Enums.PaymentStatus.Pending,
            PaymentInfo = $"Upgrade to {tier.TierName} tier",
            PaymentDate = DateTime.UtcNow
        };

        await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var url = _vnPayService.CreatePaymentUrl(context, payment.Id, payment.Amount, payment.PaymentInfo);
        return new PaymentLinkResponseDto(url);
    }

    public async Task<bool> ProcessVnPayWebhookAsync(IQueryCollection query, CancellationToken cancellationToken = default)
    {
        if (!_vnPayService.ValidateSignature(query))
        {
            return false;
        }

        var paymentIdString = query["vnp_TxnRef"].ToString();
        var responseCode = query["vnp_ResponseCode"].ToString();
        var transactionId = query["vnp_TransactionNo"].ToString();

        if (string.IsNullOrEmpty(paymentIdString) || !Guid.TryParse(paymentIdString, out var paymentId))
        {
            return false;
        }

        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return false;
        }

        payment.TransactionId = transactionId;

        if (responseCode == "00") // Success
        {
            payment.Status = Data.Enums.PaymentStatus.Completed;

            if (payment.TierId.HasValue)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(payment.UserId, cancellationToken);
                if (user is not null)
                {
                    var tier = await _unitOfWork.TierMemberships.GetByIdAsync(payment.TierId.Value, cancellationToken);
                    user.TierId = payment.TierId.Value;
                    if (tier is not null && !tier.TierName.Equals("Free", StringComparison.OrdinalIgnoreCase))
                    {
                        user.TierExpireAt = DateTime.UtcNow.AddDays(30);
                    }
                    else
                    {
                        user.TierExpireAt = null;
                    }
                    _unitOfWork.Users.Update(user);
                }
            }
        }
        else
        {
            payment.Status = Data.Enums.PaymentStatus.Failed;
        }

        _unitOfWork.Payments.Update(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<PaymentResponseDto>> GetUserPaymentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var payments = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .Where(p => p.UserId == userId)
            .AsNoTracking()
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        return payments.Select(_mapper.Map<PaymentResponseDto>).ToList();
    }

    public async Task RefundPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            throw new KeyNotFoundException($"Payment with ID {paymentId} not found.");
        }

        if (payment.Status == Data.Enums.PaymentStatus.Refunded)
        {
            throw new InvalidOperationException("Payment has already been refunded.");
        }

        if (payment.Status != Data.Enums.PaymentStatus.Completed)
        {
            throw new InvalidOperationException("Only completed payments can be refunded.");
        }

        payment.Status = Data.Enums.PaymentStatus.Refunded;
        _unitOfWork.Payments.Update(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SubjectService : ISubjectService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SubjectService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<SubjectResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var subjects = await _unitOfWork.Subjects.GetAllAsync(cancellationToken);
        return subjects.Select(_mapper.Map<SubjectResponseDto>).ToList();
    }

    public async Task<SubjectResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(id, cancellationToken);
        return subject is null ? null : _mapper.Map<SubjectResponseDto>(subject);
    }

    public async Task<SubjectResponseDto> CreateAsync(CreateSubjectRequestDto request, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Subjects
            .Query()
            .FirstOrDefaultAsync(s => s.SubjectCode == request.SubjectCode, cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException($"Subject with code '{request.SubjectCode}' already exists.");
        }

        var subject = _mapper.Map<Data.Entities.Subject>(request);
        await _unitOfWork.Subjects.AddAsync(subject, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<SubjectResponseDto>(subject);
    }

    public async Task<SubjectResponseDto> UpdateAsync(Guid id, UpdateSubjectRequestDto request, CancellationToken cancellationToken = default)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(id, cancellationToken);
        if (subject is null)
        {
            throw new KeyNotFoundException($"Subject with ID {id} not found.");
        }

        var codeConflict = await _unitOfWork.Subjects
            .Query()
            .FirstOrDefaultAsync(s => s.SubjectCode == request.SubjectCode && s.Id != id, cancellationToken);

        if (codeConflict is not null)
        {
            throw new InvalidOperationException($"Subject with code '{request.SubjectCode}' already exists.");
        }

        _mapper.Map(request, subject);
        _unitOfWork.Subjects.Update(subject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<SubjectResponseDto>(subject);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(id, cancellationToken);
        if (subject is null)
        {
            throw new KeyNotFoundException($"Subject with ID {id} not found.");
        }

        _unitOfWork.Subjects.Remove(subject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class TierMembershipService : ITierMembershipService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public TierMembershipService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<TierMembershipResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tiers = await _unitOfWork.TierMemberships.GetAllAsync(cancellationToken);
        return tiers.Select(_mapper.Map<TierMembershipResponseDto>).ToList();
    }

    public async Task<TierMembershipResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(id, cancellationToken);
        return tier is null ? null : _mapper.Map<TierMembershipResponseDto>(tier);
    }

    public async Task<TierMembershipResponseDto> CreateAsync(CreateTierMembershipRequestDto request, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.TierMemberships
            .Query()
            .FirstOrDefaultAsync(t => t.TierName == request.TierName, cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException($"Tier with name '{request.TierName}' already exists.");
        }

        var tier = _mapper.Map<Data.Entities.TierMembership>(request);
        await _unitOfWork.TierMemberships.AddAsync(tier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TierMembershipResponseDto>(tier);
    }

    public async Task<TierMembershipResponseDto> UpdateAsync(Guid id, UpdateTierMembershipRequestDto request, CancellationToken cancellationToken = default)
    {
        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(id, cancellationToken);
        if (tier is null)
        {
            throw new KeyNotFoundException($"Tier membership with ID {id} not found.");
        }

        var nameConflict = await _unitOfWork.TierMemberships
            .Query()
            .FirstOrDefaultAsync(t => t.TierName == request.TierName && t.Id != id, cancellationToken);

        if (nameConflict is not null)
        {
            throw new InvalidOperationException($"Tier with name '{request.TierName}' already exists.");
        }

        _mapper.Map(request, tier);
        _unitOfWork.TierMemberships.Update(tier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TierMembershipResponseDto>(tier);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(id, cancellationToken);
        if (tier is null)
        {
            throw new KeyNotFoundException($"Tier membership with ID {id} not found.");
        }

        _unitOfWork.TierMemberships.Remove(tier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}


