namespace AIStudyHub.Business.Exceptions;

public sealed class FileSizeLimitExceededException : Exception
{
    public FileSizeLimitExceededException(long actualBytes, long limitBytes)
        : base($"File size limit exceeded. Actual size: {actualBytes} bytes. Limit: {limitBytes} bytes.")
    {
        ActualBytes = actualBytes;
        LimitBytes = limitBytes;
    }

    public long ActualBytes { get; }

    public long LimitBytes { get; }
}
