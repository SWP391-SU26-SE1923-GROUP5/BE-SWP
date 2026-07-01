namespace AIStudyHub.Business.DTOs.Common;

/// <summary>
/// Lightweight result wrapper for service methods that need to surface a status without throwing.
/// </summary>
public sealed record ServiceResult<T>(bool Success, T? Data, string? Error)
{
    public static ServiceResult<T> Ok(T data) => new(true, data, null);
    public static ServiceResult<T> Fail(string error) => new(false, default, error);
}

public sealed record ServiceResult(bool Success, string? Error)
{
    public static ServiceResult Ok() => new(true, null);
    public static ServiceResult Fail(string error) => new(false, error);
}
