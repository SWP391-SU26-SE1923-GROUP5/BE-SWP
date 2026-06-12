namespace AIStudyHub.Business.Interfaces.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string extension, CancellationToken ct = default);
    Task<string> SaveFileAsync(byte[] fileContent, string fileName, string extension, CancellationToken ct = default);
    Task DeleteFileAsync(string relativePath, CancellationToken ct = default);
    string GetFileUrl(string relativePath);
    bool IsValidExtension(string extension);
}
