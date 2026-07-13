namespace AIStudyHub.Business.Exceptions;

public class QuotaExceededException : Exception
{
    public int CurrentUsage { get; }
    public int Limit { get; }
    public int RequestedTokens { get; }

    public QuotaExceededException(int currentUsage, int limit, int requestedTokens)
        : base($"AI token quota exceeded. Current usage: {currentUsage}/{limit} tokens. Requested: {requestedTokens} tokens.")
    {
        CurrentUsage = currentUsage;
        Limit = limit;
        RequestedTokens = requestedTokens;
    }

    public QuotaExceededException(string message) : base(message)
    {
        CurrentUsage = 0;
        Limit = 0;
        RequestedTokens = 0;
    }
}
