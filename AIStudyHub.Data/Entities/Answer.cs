namespace AIStudyHub.Data.Entities;

public sealed class Answer : BaseEntity
{
    public Guid QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }

    public Question Question { get; set; } = null!;
}
