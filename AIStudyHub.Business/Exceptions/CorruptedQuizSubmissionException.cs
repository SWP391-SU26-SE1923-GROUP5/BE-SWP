namespace AIStudyHub.Business.Exceptions;

public sealed class CorruptedQuizSubmissionException : Exception
{
    public CorruptedQuizSubmissionException(Guid submissionId)
        : base("Stored quiz answers are invalid.")
    {
        SubmissionId = submissionId;
    }

    public Guid SubmissionId { get; }
}
