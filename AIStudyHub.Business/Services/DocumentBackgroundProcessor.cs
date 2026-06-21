using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

public class DocumentBackgroundProcessor : BackgroundService
{
    private readonly IDocumentProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentBackgroundProcessor> _logger;

    public DocumentBackgroundProcessor(
        IDocumentProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentBackgroundProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document Background Processor started");

        try
        {
            await foreach (var request in _queue.DequeueAsync(stoppingToken))
            {
                try
                {
                    await ProcessDocumentAsync(request, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing document {DocumentId}", request.DocumentId);
                    await HandleFailureAsync(request, ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Document Background Processor stopping");
        }
    }

    private async Task ProcessDocumentAsync(DocumentProcessRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Processing document {DocumentId} for user {UserId}",
            request.DocumentId, request.UserId);

        using var scope = _scopeFactory.CreateScope();
        
        var scopeLogger = scope.ServiceProvider.GetRequiredService<ILogger<DocumentBackgroundProcessor>>();
        scopeLogger.LogInformation("Document {DocumentId} processing delegated to KernelMemory", request.DocumentId);
        
        // TODO: Wire up to KernelMemoryService in Task 3.1
        // var kernelMemory = scope.ServiceProvider.GetRequiredService<IKernelMemory>();
        // await kernelMemory.ImportDocumentAsync(...);

        _logger.LogInformation("Document {DocumentId} processed successfully", request.DocumentId);
    }

    private Task HandleFailureAsync(DocumentProcessRequest request, Exception ex)
    {
        _logger.LogWarning("Document {DocumentId} failed: {Error}",
            request.DocumentId, ex.Message);
        return Task.CompletedTask;
    }
}
