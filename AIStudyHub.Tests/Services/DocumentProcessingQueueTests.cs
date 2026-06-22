using AIStudyHub.Business.Services;

namespace AIStudyHub.Tests.Services;

public class DocumentProcessingQueueTests
{
    [Fact]
    public async Task EnqueueAsync_ShouldMakeItemAvailableForDequeue()
    {
        // Arrange
        var queue = new DocumentProcessingQueue(NullLogger<DocumentProcessingQueue>.Instance);
        var request = new DocumentProcessRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "/path/to/file.pdf",
            "test.pdf",
            "application/pdf");

        // Act
        await queue.EnqueueAsync(request);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        DocumentProcessRequest? result = null;
        
        await foreach (var item in queue.DequeueAsync(cts.Token))
        {
            result = item;
            break;
        }

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.DocumentId, result.DocumentId);
        Assert.Equal(request.FileName, result.FileName);
    }

    [Fact]
    public async Task DequeueAsync_ShouldWaitWhenEmpty()
    {
        // Arrange
        var queue = new DocumentProcessingQueue(NullLogger<DocumentProcessingQueue>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var hasItems = false;
        await foreach (var _ in queue.DequeueAsync(cts.Token))
        {
            hasItems = true;
            break;
        }
        
        Assert.False(hasItems);
    }
}
