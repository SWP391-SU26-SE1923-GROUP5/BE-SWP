using Microsoft.KernelMemory;

namespace AIStudyHub.Business.Services;

public interface IKernelMemoryService
{
    Task<string> ImportDocumentAsync(string filePath, Guid documentId, Guid userId, string fileName, CancellationToken ct = default);
    Task<IEnumerable<Citation>> SearchAsync(string query, Guid userId, int topK = 10, CancellationToken ct = default);
    Task<MemoryAnswer> AskAsync(string question, Guid userId, CancellationToken ct = default);
}
