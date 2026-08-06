namespace AIStudyHub.Business.Exceptions;

public sealed class StorageQuotaExceededException : Exception
{
    public StorageQuotaExceededException(
        long currentBytes,
        long limitBytes,
        long requestedBytes)
        : base(
            $"Storage quota exceeded. Current usage: {currentBytes} bytes. "
            + $"Limit: {limitBytes} bytes. Requested: {requestedBytes} bytes.")
    {
        CurrentBytes = currentBytes;
        LimitBytes = limitBytes;
        RequestedBytes = requestedBytes;
    }

    public long CurrentBytes { get; }

    public long LimitBytes { get; }

    public long RequestedBytes { get; }
}
