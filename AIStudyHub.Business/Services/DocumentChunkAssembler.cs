using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Enums;
using AIStudyHub.Business.Interfaces.Services;

namespace AIStudyHub.Business.Services;

public static class DocumentChunkAssembler
{
    public static async Task<List<DocumentChunkDto>> AssembleAsync(
        IDocumentProcessingService processor,
        IReadOnlyList<ExtractedTextSegment> segments,
        string? summary,
        int chunkSize,
        int overlap,
        bool preserveTables = true)
    {
        var chunks = await processor.ChunkSegmentsAsync(
            segments, chunkSize, overlap, preserveTables);

        if (!string.IsNullOrWhiteSpace(summary))
        {
            chunks.Insert(0, new DocumentChunkDto
            {
                Text = summary.Trim(),
                PageNumber = null,
                ContentType = DocumentContentType.Summary
            });
        }

        return chunks;
    }
}
