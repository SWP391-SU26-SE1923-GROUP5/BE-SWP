using System.Threading.Channels;
using AIStudyHub.Business.DTOs.Documents;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

public interface IDocumentProcessingQueue
{
    ValueTask EnqueueAsync(DocumentProcessRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<DocumentProcessRequest> DequeueAsync(CancellationToken cancellationToken = default);
}

public class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<DocumentProcessRequest> _channel;
    private readonly ILogger<DocumentProcessingQueue> _logger;

    public DocumentProcessingQueue(ILogger<DocumentProcessingQueue> logger, int capacity = 100)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<DocumentProcessRequest>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask EnqueueAsync(DocumentProcessRequest request, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(request, cancellationToken);
        _logger.LogInformation("Document {DocumentId} queued for processing", request.DocumentId);
    }

    public async IAsyncEnumerable<DocumentProcessRequest> DequeueAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var request))
            {
                yield return request;
            }
        }
    }
}
