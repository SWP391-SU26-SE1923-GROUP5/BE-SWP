using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.AI.Orchestration;
using Microsoft.KernelMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AIStudyHub.Business.Interfaces.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace AIStudyHub.Business.AI.Orchestration;

public class KernelMemoryService : IKernelMemoryService
{
    private readonly IKernelMemory _memory;
    private readonly ILogger<KernelMemoryService> _logger;

    public KernelMemoryService(
        IKernelMemory memory,
        ILogger<KernelMemoryService> logger)
    {
        _memory = memory;
        _logger = logger;
    }

    public async Task<string> ImportDocumentAsync(
        string filePath,
        Guid documentId,
        Guid userId,
        string fileName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Importing document {DocumentId} to Kernel Memory", documentId);

        var documentIdStr = documentId.ToString();
        
        var tags = new TagCollection
        {
            ["user_id"] = new List<string?> { userId.ToString() },
            ["file_name"] = new List<string?> { fileName }
        };
        
        await _memory.ImportDocumentAsync(
            filePath,
            documentId: documentIdStr,
            steps: Constants.PipelineWithoutSummary,
            tags: tags);

        _logger.LogInformation("Document {DocumentId} imported successfully", documentId);
        return documentIdStr;
    }

    public async Task<IEnumerable<Citation>> SearchAsync(
        string query,
        Guid userId,
        int topK = 10,
        CancellationToken ct = default)
    {
        var filter = MemoryFilters.ByTag("user_id", userId.ToString());

        var result = await _memory.SearchAsync(
            query,
            filter: filter,
            limit: topK,
            cancellationToken: ct);

        return result.Results;
    }

    public async Task<MemoryAnswer> AskAsync(
        string question,
        Guid userId,
        CancellationToken ct = default)
    {
        var filter = MemoryFilters.ByTag("user_id", userId.ToString());

        var result = await _memory.AskAsync(
            question,
            filter: filter,
            cancellationToken: ct);

        return result;
    }
}
