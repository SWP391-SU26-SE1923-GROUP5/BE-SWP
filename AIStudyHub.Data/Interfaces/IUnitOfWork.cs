using AIStudyHub.Data.Entities;

namespace AIStudyHub.Data.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Subject> Subjects { get; }
    IRepository<TierMembership> TierMemberships { get; }
    IRepository<Document> Documents { get; }
    IDocumentChunkRepository DocumentChunks { get; }
    IRepository<Vote> Votes { get; }
    IRepository<Report> Reports { get; }
    IRepository<Flashcard> Flashcards { get; }
    IRepository<Quiz> Quizzes { get; }
    IRepository<Question> Questions { get; }
    IRepository<Answer> Answers { get; }
    IRepository<QuizSubmission> QuizSubmissions { get; }
    IRepository<Notification> Notifications { get; }
    IRepository<Payment> Payments { get; }
    IRepository<ChatSession> ChatSessions { get; }
    IRepository<ChatMessage> ChatMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
