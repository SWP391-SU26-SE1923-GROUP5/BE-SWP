using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Workers;

public sealed class DocumentReindexWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDocumentProcessingQueue _queue;
    private readonly DocumentReindexOptions _options;
    private readonly DocumentStorageOptions _storage;
    private readonly ILogger<DocumentReindexWorker> _logger;

    public DocumentReindexWorker(
        IServiceScopeFactory scopeFactory,
        IDocumentProcessingQueue queue,
        IOptions<DocumentReindexOptions> options,
        IOptions<DocumentStorageOptions> storage,
        ILogger<DocumentReindexWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options.Value;
        _storage = storage.Value;
        _logger = logger;
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var claimService = scope.ServiceProvider.GetRequiredService<IDocumentReindexClaimService>();
        var claims = await claimService.ClaimBatchAsync(
            _options.BatchSize,
            TimeSpan.FromMinutes(_options.ClaimTimeoutMinutes),
            _options.MaxAttempts,
            ct);

        var queued = 0;
        foreach (var claim in claims)
        {
            try
            {
                var filePath = ResolveSourcePath(claim.FileLink);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Document source file not found.");

                var request = new DocumentProcessRequest(
                    claim.DocumentId,
                    claim.UserId,
                    filePath,
                    claim.FileName,
                    claim.ContentType,
                    IndexRunId: claim.IndexRunId,
                    IsReindex: true,
                    ReindexClaimId: claim.ClaimId);

                await _queue.EnqueueAsync(request, ct);
                queued++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not enqueue document {DocumentId} for reindex", claim.DocumentId);
                await claimService.FailClaimAsync(claim.DocumentId, claim.ClaimId, ex.Message, ct);
            }
        }

        return queued;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Automatic document reindexing is disabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var queued = await RunOnceAsync(stoppingToken);
                if (queued > 0)
                    _logger.LogInformation("Queued {Count} document(s) for reindex", queued);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic document reindex scan failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.ScanIntervalMinutes)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private string ResolveSourcePath(string fileLink)
    {
        var relativePath = fileLink.Replace('\\', '/').TrimStart('/');
        if (relativePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath["uploads/".Length..];

        var root = Path.GetFullPath(_storage.BasePath);
        var source = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!source.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Document source path is outside the configured storage root.");

        return source;
    }
}
