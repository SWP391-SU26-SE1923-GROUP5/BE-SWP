using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Services;
using AIStudyHub.Business.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AIStudyHub.Tests.Services;

public sealed class DocumentReindexWorkerTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Mock<IDocumentReindexClaimService> _claims = new();
    private readonly Mock<IDocumentProcessingQueue> _queue = new();
    private readonly ServiceProvider _provider;

    public DocumentReindexWorkerTests()
    {
        Directory.CreateDirectory(_storageRoot);
        var services = new ServiceCollection();
        services.AddSingleton(_claims.Object);
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunOnceAsync_ExistingSource_EnqueuesClaimedDocument()
    {
        var relative = Path.Combine("2026", "07", "source.pdf");
        Directory.CreateDirectory(Path.Combine(_storageRoot, "2026", "07"));
        await File.WriteAllTextAsync(Path.Combine(_storageRoot, relative), "pdf");
        var claim = NewClaim($"/uploads/{relative.Replace('\\', '/')}");
        _claims.Setup(x => x.ClaimBatchAsync(10, It.IsAny<TimeSpan>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync([claim]);
        var worker = CreateWorker();

        var queued = await worker.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, queued);
        _queue.Verify(x => x.EnqueueAsync(
            It.Is<DocumentProcessRequest>(r => r.DocumentId == claim.DocumentId
                && r.IndexRunId == claim.IndexRunId && r.IsReindex
                && r.ReindexClaimId == claim.ClaimId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_MissingSource_ReleasesClaimAsFailed()
    {
        var claim = NewClaim("/uploads/missing.pdf");
        _claims.Setup(x => x.ClaimBatchAsync(10, It.IsAny<TimeSpan>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync([claim]);
        var worker = CreateWorker();

        var queued = await worker.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, queued);
        _claims.Verify(x => x.FailClaimAsync(
            claim.DocumentId, claim.ClaimId, It.Is<string>(s => s.Contains("not found")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private DocumentReindexWorker CreateWorker() => new(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        _queue.Object,
        Options.Create(new DocumentReindexOptions()),
        Options.Create(new DocumentStorageOptions { BasePath = _storageRoot }),
        Mock.Of<ILogger<DocumentReindexWorker>>());

    private static DocumentReindexClaim NewClaim(string link) => new(
        Guid.NewGuid(), Guid.NewGuid(), link, "source.pdf", "application/pdf",
        Guid.NewGuid(), Guid.NewGuid());

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_storageRoot, true);
    }
}
