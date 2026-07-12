using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class TokenLedger : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? RelatedEntityId { get; set; } // QuizId or DocumentId
    public string OperationType { get; set; } = string.Empty; // "GenerateQuiz" | "GenerateFlashcards"
    public TokenLedgerStatus Status { get; set; } = TokenLedgerStatus.Reserved;
    public int EstimatedTokens { get; set; }
    public int? ActualTokens { get; set; }
    public string? FailureReason { get; set; }

    public User User { get; set; } = null!;
}
