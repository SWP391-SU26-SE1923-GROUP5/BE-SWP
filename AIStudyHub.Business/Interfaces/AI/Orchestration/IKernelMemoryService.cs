using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.KernelMemory;

namespace AIStudyHub.Business.Interfaces.AI.Orchestration;

public interface IKernelMemoryService
{
    Task<string> ImportDocumentAsync(string filePath, Guid documentId, Guid userId, string fileName, CancellationToken ct = default);
    Task<IEnumerable<Citation>> SearchAsync(string query, Guid userId, int topK = 10, CancellationToken ct = default);
    Task<MemoryAnswer> AskAsync(string question, Guid userId, CancellationToken ct = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default);
}
