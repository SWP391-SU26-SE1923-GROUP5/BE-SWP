namespace AIStudyHub.Data.Entities;

public sealed class Quiz : BaseEntity
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public decimal PassingScore { get; set; }

    public Document Document { get; set; } = null!;
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<QuizSubmission> QuizSubmissions { get; set; } = new List<QuizSubmission>();
}
