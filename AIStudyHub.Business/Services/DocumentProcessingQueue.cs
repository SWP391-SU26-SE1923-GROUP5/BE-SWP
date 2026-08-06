using System.Collections.Concurrent;
using System.Threading.Channels;
using AIStudyHub.Business.DTOs.Documents;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

public interface IDocumentProcessingQueue
{
    bool TryEnqueue(DocumentProcessRequest request);
    IAsyncEnumerable<DocumentProcessRequest> DequeueAsync(CancellationToken cancellationToken = default);
    void Complete(Guid documentId);
}

public class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<DocumentProcessRequest> _channel =
        Channel.CreateUnbounded<DocumentProcessRequest>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
    private readonly ConcurrentDictionary<Guid, byte> _queuedDocumentIds = new();
    private readonly Dictionary<Guid, DocumentProcessRequest> _pendingRequests = new();
    private readonly object _queueLock = new();
    private readonly ILogger<DocumentProcessingQueue> _logger;

    public DocumentProcessingQueue(ILogger<DocumentProcessingQueue> logger)
    {
        _logger = logger;
    }

    public bool TryEnqueue(DocumentProcessRequest request)
    {
        lock (_queueLock)
        {
            if (!_queuedDocumentIds.TryAdd(request.DocumentId, 0))
            {
                if (request.IsReprocess)
                    _pendingRequests[request.DocumentId] = request;

                _logger.LogDebug(
                    "Document {DocumentId} is already queued for processing",
                    request.DocumentId);
                return false;
            }

            if (_channel.Writer.TryWrite(request))
            {
                _logger.LogInformation(
                    "Document {DocumentId} queued for processing",
                    request.DocumentId);
                return true;
            }

            _queuedDocumentIds.TryRemove(request.DocumentId, out _);
            _logger.LogWarning(
                "Document {DocumentId} could not be queued for processing",
                request.DocumentId);
            return false;
        }
    }

    public void Complete(Guid documentId)
    {
        lock (_queueLock)
        {
            _queuedDocumentIds.TryRemove(documentId, out _);
            if (!_pendingRequests.Remove(documentId, out var pendingRequest))
                return;

            if (!_queuedDocumentIds.TryAdd(documentId, 0))
            {
                _pendingRequests[documentId] = pendingRequest;
                _logger.LogError(
                    "Could not reserve document {DocumentId} for its pending processing request",
                    documentId);
                return;
            }

            if (_channel.Writer.TryWrite(pendingRequest))
            {
                _logger.LogInformation(
                    "Document {DocumentId} requeued with its latest pending processing request",
                    documentId);
                return;
            }

            _queuedDocumentIds.TryRemove(documentId, out _);
            _logger.LogWarning(
                "Pending request for document {DocumentId} could not be queued for processing",
                documentId);
        }
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
