using AIStudyHub.Business.Enums;

namespace AIStudyHub.Business.Entities;

public sealed class Document : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public User User { get; set; } = null!;
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
}
