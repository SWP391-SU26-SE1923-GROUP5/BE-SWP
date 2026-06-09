namespace AIStudyHub.Data.Entities;

public sealed class QuizSubmission : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid QuizId { get; set; }
    public string Answers { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Quiz Quiz { get; set; } = null!;
}
