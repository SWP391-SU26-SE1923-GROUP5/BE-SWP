using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class Question : BaseEntity
{
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public QuestionType Type { get; set; }= QuestionType.SingleChoice;
    public int Position { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}
