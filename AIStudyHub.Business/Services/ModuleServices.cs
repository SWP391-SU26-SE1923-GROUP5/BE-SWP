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
using AIStudyHub.Business.DTOs.Votes;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed class DocumentService : CrudService<DocumentResponseDto, CreateDocumentRequestDto, UpdateDocumentRequestDto>, IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public DocumentService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<DocumentResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return documents.Select(_mapper.Map<DocumentResponseDto>).ToList();
    }

    public override async Task<DocumentResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return document is null ? null : _mapper.Map<DocumentResponseDto>(document);
    }

    public override async Task<DocumentResponseDto> CreateAsync(CreateDocumentRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task<DocumentResponseDto> UpdateAsync(Guid id, UpdateDocumentRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document with ID {id} not found.");
        }

        _unitOfWork.Documents.Remove(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class VoteService : CrudService<VoteResponseDto, CreateVoteRequestDto, UpdateVoteRequestDto>, IVoteService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public VoteService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<VoteResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var votes = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return votes.Select(_mapper.Map<VoteResponseDto>).ToList();
    }

    public override async Task<VoteResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vote = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        return vote is null ? null : _mapper.Map<VoteResponseDto>(vote);
    }

    public override async Task<VoteResponseDto> CreateAsync(CreateVoteRequestDto request, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Votes
            .Query()
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.DocumentId == request.DocumentId, cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException($"User has already voted on this document.");
        }

        var documentExists = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId, cancellationToken) is not null;
        if (!documentExists)
        {
            throw new KeyNotFoundException($"Document with ID {request.DocumentId} not found.");
        }

        var vote = _mapper.Map<Data.Entities.Vote>(request);
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

    public override async Task<VoteResponseDto> UpdateAsync(Guid id, UpdateVoteRequestDto request, CancellationToken cancellationToken = default)
    {
        var vote = await _unitOfWork.Votes.GetByIdAsync(id, cancellationToken);
        if (vote is null)
        {
            throw new KeyNotFoundException($"Vote with ID {id} not found.");
        }

        _mapper.Map(request, vote);
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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

public sealed class ReportService : CrudService<ReportResponseDto, CreateReportRequestDto, UpdateReportRequestDto>, IReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ReportService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<ReportResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var reports = await _unitOfWork.Reports
            .Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reports.Select(_mapper.Map<ReportResponseDto>).ToList();
    }

    public override async Task<ReportResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await _unitOfWork.Reports
            .Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return report is null ? null : _mapper.Map<ReportResponseDto>(report);
    }

    public override async Task<ReportResponseDto> CreateAsync(CreateReportRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task<ReportResponseDto> UpdateAsync(Guid id, UpdateReportRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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

public sealed class FlashcardService : CrudService<FlashcardResponseDto, CreateFlashcardRequestDto, UpdateFlashcardRequestDto>, IFlashcardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public FlashcardService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<FlashcardResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var flashcards = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return flashcards.Select(_mapper.Map<FlashcardResponseDto>).ToList();
    }

    public override async Task<FlashcardResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var flashcard = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        return flashcard is null ? null : _mapper.Map<FlashcardResponseDto>(flashcard);
    }

    public override async Task<FlashcardResponseDto> CreateAsync(CreateFlashcardRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task<FlashcardResponseDto> UpdateAsync(Guid id, UpdateFlashcardRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var flashcard = await _unitOfWork.Flashcards.GetByIdAsync(id, cancellationToken);
        if (flashcard is null)
        {
            throw new KeyNotFoundException($"Flashcard with ID {id} not found.");
        }

        _unitOfWork.Flashcards.Remove(flashcard);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class QuizService : CrudService<QuizResponseDto, CreateQuizRequestDto, UpdateQuizRequestDto>, IQuizService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public QuizService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<QuizResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var quizzes = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return quizzes.Select(_mapper.Map<QuizResponseDto>).ToList();
    }

    public override async Task<QuizResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quiz = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return quiz is null ? null : _mapper.Map<QuizResponseDto>(quiz);
    }

    public override async Task<QuizResponseDto> CreateAsync(CreateQuizRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task<QuizResponseDto> UpdateAsync(Guid id, UpdateQuizRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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

public sealed class QuestionService : CrudService<QuestionResponseDto, CreateQuestionRequestDto, UpdateQuestionRequestDto>, IQuestionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public QuestionService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<QuestionResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var questions = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return questions.Select(_mapper.Map<QuestionResponseDto>).ToList();
    }

    public override async Task<QuestionResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var question = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return question is null ? null : _mapper.Map<QuestionResponseDto>(question);
    }

    public override async Task<QuestionResponseDto> CreateAsync(CreateQuestionRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task<QuestionResponseDto> UpdateAsync(Guid id, UpdateQuestionRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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

public sealed class AnswerService : CrudService<AnswerResponseDto, CreateAnswerRequestDto, UpdateAnswerRequestDto>, IAnswerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public AnswerService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<AnswerResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var answers = await _unitOfWork.Answers
            .Query()
            .Include(a => a.Question)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return answers.Select(_mapper.Map<AnswerResponseDto>).ToList();
    }

    public override async Task<AnswerResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var answer = await _unitOfWork.Answers
            .Query()
            .Include(a => a.Question)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return answer is null ? null : _mapper.Map<AnswerResponseDto>(answer);
    }

    public override async Task<AnswerResponseDto> CreateAsync(CreateAnswerRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task<AnswerResponseDto> UpdateAsync(Guid id, UpdateAnswerRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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

public sealed class QuizSubmissionService : CrudService<QuizSubmissionResponseDto, CreateQuizSubmissionRequestDto, UpdateQuizSubmissionRequestDto>, IQuizSubmissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public QuizSubmissionService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<QuizSubmissionResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var submissions = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.User)
            .Include(qs => qs.Quiz)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return submissions.Select(_mapper.Map<QuizSubmissionResponseDto>).ToList();
    }

    public override async Task<QuizSubmissionResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var submission = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.User)
            .Include(qs => qs.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(qs => qs.Id == id, cancellationToken);

        return submission is null ? null : _mapper.Map<QuizSubmissionResponseDto>(submission);
    }

    public override async Task<QuizSubmissionResponseDto> CreateAsync(CreateQuizSubmissionRequestDto request, CancellationToken cancellationToken = default)
    {
        var quizExists = await _unitOfWork.Quizzes.GetByIdAsync(request.QuizId, cancellationToken) is not null;
        if (!quizExists)
        {
            throw new KeyNotFoundException($"Quiz with ID {request.QuizId} not found.");
        }

        var submission = _mapper.Map<Data.Entities.QuizSubmission>(request);
        submission.SubmittedAt = DateTime.UtcNow;
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

    public override async Task<QuizSubmissionResponseDto> UpdateAsync(Guid id, UpdateQuizSubmissionRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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

public sealed class NotificationService : CrudService<NotificationResponseDto, CreateNotificationRequestDto, UpdateNotificationRequestDto>, INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public NotificationService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<NotificationResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await _unitOfWork.Notifications
            .Query()
            .Include(n => n.User)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return notifications.Select(_mapper.Map<NotificationResponseDto>).ToList();
    }

    public override async Task<NotificationResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications
            .Query()
            .Include(n => n.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        return notification is null ? null : _mapper.Map<NotificationResponseDto>(notification);
    }

    public override async Task<NotificationResponseDto> CreateAsync(CreateNotificationRequestDto request, CancellationToken cancellationToken = default)
    {
        var notification = _mapper.Map<Data.Entities.Notification>(request);
        await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Notifications
            .Query()
            .Include(n => n.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notification.Id, cancellationToken);

        return _mapper.Map<NotificationResponseDto>(created);
    }

    public override async Task<NotificationResponseDto> UpdateAsync(Guid id, UpdateNotificationRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            throw new KeyNotFoundException($"Notification with ID {id} not found.");
        }

        _unitOfWork.Notifications.Remove(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class PaymentService : CrudService<PaymentResponseDto, CreatePaymentRequestDto, UpdatePaymentRequestDto>, IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public PaymentService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<PaymentResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var payments = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return payments.Select(_mapper.Map<PaymentResponseDto>).ToList();
    }

    public override async Task<PaymentResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return payment is null ? null : _mapper.Map<PaymentResponseDto>(payment);
    }

    public override async Task<PaymentResponseDto> CreateAsync(CreatePaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.TierId.HasValue)
        {
            var tierExists = await _unitOfWork.TierMemberships.GetByIdAsync(request.TierId.Value, cancellationToken) is not null;
            if (!tierExists)
            {
                throw new KeyNotFoundException($"Tier membership with ID {request.TierId} not found.");
            }
        }

        var payment = _mapper.Map<Data.Entities.Payment>(request);
        await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == payment.Id, cancellationToken);

        return _mapper.Map<PaymentResponseDto>(created);
    }

    public override async Task<PaymentResponseDto> UpdateAsync(Guid id, UpdatePaymentRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(id, cancellationToken);
        if (payment is null)
        {
            throw new KeyNotFoundException($"Payment with ID {id} not found.");
        }

        _unitOfWork.Payments.Remove(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SubjectService : CrudService<SubjectResponseDto, CreateSubjectRequestDto, UpdateSubjectRequestDto>, ISubjectService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SubjectService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public override async Task<IReadOnlyList<SubjectResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var subjects = await _unitOfWork.Subjects.GetAllAsync(cancellationToken);
        return subjects.Select(_mapper.Map<SubjectResponseDto>).ToList();
    }

    public override async Task<SubjectResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(id, cancellationToken);
        return subject is null ? null : _mapper.Map<SubjectResponseDto>(subject);
    }

    public override async Task<SubjectResponseDto> CreateAsync(CreateSubjectRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task<SubjectResponseDto> UpdateAsync(Guid id, UpdateSubjectRequestDto request, CancellationToken cancellationToken = default)
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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
