using AIStudyHub.Data.Entities;

namespace AIStudyHub.Data.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Subject> Subjects { get; }
    IRepository<TierMembership> TierMemberships { get; }
    IRepository<Document> Documents { get; }
    IRepository<Vote> Votes { get; }
    IRepository<Report> Reports { get; }
    IRepository<Flashcard> Flashcards { get; }
    IRepository<FlashcardDeck> FlashcardDecks { get; }
    IRepository<FlashcardReview> FlashcardReviews { get; }
    IRepository<FlashcardReviewAttempt> FlashcardReviewAttempts { get; }
    IRepository<Quiz> Quizzes { get; }
    IRepository<Question> Questions { get; }
    IRepository<Answer> Answers { get; }
    IRepository<QuizSubmission> QuizSubmissions { get; }
    IRepository<Notification> Notifications { get; }
    IRepository<Payment> Payments { get; }
    IRepository<ChatSession> ChatSessions { get; }
    IRepository<ChatMessage> ChatMessages { get; }
    IRepository<ChatSessionDocument> ChatSessionDocuments { get; }
    IRepository<UserStats> UserStats { get; }
    IRepository<StudyLog> StudyLogs { get; }
    IRepository<Badge> Badges { get; }
    IRepository<UserBadge> UserBadges { get; }
    IRepository<DocumentShare> DocumentShares { get; }
    IRepository<TokenLedger> TokenLedgers { get; }
    IRepository<Recommendation> Recommendations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
