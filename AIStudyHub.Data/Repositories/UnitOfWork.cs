using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;

namespace AIStudyHub.Data.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    public UnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        Users = new GenericRepository<User>(_dbContext);
        Subjects = new GenericRepository<Subject>(_dbContext);
        TierMemberships = new GenericRepository<TierMembership>(_dbContext);
        Documents = new GenericRepository<Document>(_dbContext);
        Votes = new GenericRepository<Vote>(_dbContext);
        Reports = new GenericRepository<Report>(_dbContext);
        Flashcards = new GenericRepository<Flashcard>(_dbContext);
        Quizzes = new GenericRepository<Quiz>(_dbContext);
        Questions = new GenericRepository<Question>(_dbContext);
        Answers = new GenericRepository<Answer>(_dbContext);
        QuizSubmissions = new GenericRepository<QuizSubmission>(_dbContext);
        Notifications = new GenericRepository<Notification>(_dbContext);
        Payments = new GenericRepository<Payment>(_dbContext);
        ChatSessions = new GenericRepository<ChatSession>(_dbContext);
        ChatMessages = new GenericRepository<ChatMessage>(_dbContext);
    }

    public IRepository<User> Users { get; }
    public IRepository<Subject> Subjects { get; }
    public IRepository<TierMembership> TierMemberships { get; }
    public IRepository<Document> Documents { get; }
    public IRepository<Vote> Votes { get; }
    public IRepository<Report> Reports { get; }
    public IRepository<Flashcard> Flashcards { get; }
    public IRepository<Quiz> Quizzes { get; }
    public IRepository<Question> Questions { get; }
    public IRepository<Answer> Answers { get; }
    public IRepository<QuizSubmission> QuizSubmissions { get; }
    public IRepository<Notification> Notifications { get; }
    public IRepository<Payment> Payments { get; }
    public IRepository<ChatSession> ChatSessions { get; }
    public IRepository<ChatMessage> ChatMessages { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
