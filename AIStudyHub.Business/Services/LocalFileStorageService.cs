using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly DocumentStorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _baseDirectory;

    public LocalFileStorageService(
        IOptions<DocumentStorageOptions> options,
        ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _baseDirectory = Path.GetFullPath(_options.BasePath);
    }

    public async Task<string> SaveFileAsync(
        Stream fileStream,
        string fileName,
        string extension,
        CancellationToken ct = default)
    {
        var relativePath = GenerateRelativePath(fileName, extension);
        var fullPath = Path.Combine(_baseDirectory, relativePath);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStreamOut = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(fileStreamOut, ct);

        _logger.LogDebug("File saved to {Path}", relativePath);
        return relativePath;
    }

    public async Task<string> SaveFileAsync(
        byte[] fileContent,
        string fileName,
        string extension,
        CancellationToken ct = default)
    {
        await using var stream = new MemoryStream(fileContent);
        return await SaveFileAsync(stream, fileName, extension, ct);
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_baseDirectory, relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("File deleted: {Path}", relativePath);
        }

        return Task.CompletedTask;
    }

    public string GetFileUrl(string relativePath)
    {
        return $"/uploads/{relativePath.Replace('\\', '/')}";
    }

    public bool IsValidExtension(string extension)
    {
        return _options.AllowedExtensions.Contains(
            extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}");
    }

    private string GenerateRelativePath(string fileName, string extension)
    {
        var now = DateTime.UtcNow;
        var sanitizedFileName = SanitizeFileName(fileName);
        var ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";

        return Path.Combine(
            now.Year.ToString(),
            now.Month.ToString("D2"),
            $"{Guid.NewGuid():N}_{sanitizedFileName}{ext}");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }
}
