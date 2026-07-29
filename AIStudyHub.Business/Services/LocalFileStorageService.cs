using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Exceptions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private const int FileCreateAttempts = 3;
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

    public async Task<StoredFileResult> SaveFileAsync(
        Stream fileStream,
        string fileName,
        string extension,
        long maxFileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= FileCreateAttempts; attempt++)
        {
            var relativePath = GenerateRelativePath(fileName, extension);
            var fullPath = ResolveFullPath(relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)
                && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var buffer = new byte[81920];
            var actualBytes = 0L;
            var outputCreated = false;

            try
            {
                await using (var destination = new FileStream(
                                 fullPath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 buffer.Length,
                                 FileOptions.Asynchronous))
                {
                    outputCreated = true;
                    while (true)
                    {
                        var remainingAllowedBytes =
                            maxFileSizeBytes - actualBytes;
                        var bytesToRead = remainingAllowedBytes > 0
                            ? (int)Math.Min(
                                buffer.Length,
                                remainingAllowedBytes)
                            : 1;
                        var bytesRead = await fileStream.ReadAsync(
                            buffer.AsMemory(0, bytesToRead),
                            cancellationToken);

                        if (bytesRead == 0)
                            break;

                        actualBytes += bytesRead;
                        if (actualBytes > maxFileSizeBytes)
                        {
                            throw new FileSizeLimitExceededException(
                                actualBytes,
                                maxFileSizeBytes);
                        }

                        await destination.WriteAsync(
                            buffer.AsMemory(0, bytesRead),
                            cancellationToken);
                    }
                }

                _logger.LogDebug("File saved to {Path}", relativePath);
                return new StoredFileResult(relativePath, actualBytes);
            }
            catch (IOException) when (!outputCreated
                && File.Exists(fullPath)
                && attempt < FileCreateAttempts)
            {
                _logger.LogWarning(
                    "Generated storage path collided on attempt {Attempt}; retrying",
                    attempt);
            }
            catch
            {
                if (outputCreated && File.Exists(fullPath))
                    File.Delete(fullPath);

                throw;
            }
        }

        throw new IOException(
            $"Could not allocate a unique storage path after {FileCreateAttempts} attempts.");
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(relativePath);

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

    public string ResolveFullPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, relativePath));
        var relativeToBase = Path.GetRelativePath(_baseDirectory, fullPath);
        if (Path.IsPathRooted(relativeToBase)
            || relativeToBase == ".."
            || relativeToBase.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved file path is outside document storage.");
        }

        return fullPath;
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
