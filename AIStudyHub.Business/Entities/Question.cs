using AIStudyHub.Business.Enums;

namespace AIStudyHub.Business.Entities;

public sealed class Question : BaseEntity
{
    public Guid QuizId { get; set; }
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    public int SortOrder { get; set; }
    public decimal Points { get; set; } = 1;

    public Quiz Quiz { get; set; } = null!;
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}
