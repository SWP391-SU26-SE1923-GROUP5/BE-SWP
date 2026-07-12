using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class Document : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FileLink { get; set; }
    public string? FileName { get; set; }
    public string? FileExtension { get; set; }
    public string? FileType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? SharedUsers { get; set; }
    public string ShareStatus { get; set; } = "private";
    public DocumentStatus? Status { get; set; }
    public bool IsNonFlaggable { get; set; } = false;
    public bool? IsOcrApplied { get; set; }

    /// <summary>When Status == Failed, captures the exception message from the processing
    /// pipeline (PDF extraction, OCR, AI extraction). Null otherwise. Added 2026-06-29 per Master Spec.</summary>
    public string? ErrorMessage { get; set; }

    public User User { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    public ICollection<ChatSessionDocument> ChatSessionDocuments { get; set; } = new List<ChatSessionDocument>();
}
