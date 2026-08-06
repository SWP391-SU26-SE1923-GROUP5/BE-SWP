namespace AIStudyHub.Business.Interfaces.Services;

public sealed record StoredFileResult(string RelativePath, long SizeBytes);

public interface IFileStorageService
{
    Task<StoredFileResult> SaveFileAsync(
        Stream fileStream,
        string fileName,
        string extension,
        long maxFileSizeBytes,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string relativePath, CancellationToken ct = default);
    string GetFileUrl(string relativePath);
    string ResolveFullPath(string relativePath);
    bool IsValidExtension(string extension);
}
