namespace AIStudyHub.Business.Services;

public interface IRagPipelineService
{
    Task MarkDocumentAsProcessedAsync(Guid documentId, CancellationToken ct = default);
    Task<string> AskAsync(Guid userId, string question, CancellationToken ct = default);
}
