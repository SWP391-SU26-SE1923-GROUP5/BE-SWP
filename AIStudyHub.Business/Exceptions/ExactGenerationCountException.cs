namespace AIStudyHub.Business.Exceptions;

public sealed class ExactGenerationCountException : Exception
{
    public ExactGenerationCountException(int requestedCount, int generatedCount)
        : base($"AI generated {generatedCount} valid items but {requestedCount} were required.")
    {
        RequestedCount = requestedCount;
        GeneratedCount = generatedCount;
    }

    public int RequestedCount { get; }

    public int GeneratedCount { get; }
}
