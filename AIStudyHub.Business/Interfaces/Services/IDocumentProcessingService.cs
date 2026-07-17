using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.DTOs.Documents;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IDocumentProcessingService
{
    Task<string> ExtractTextAsync(byte[] fileContent, string fileExtension);
    Task<IReadOnlyList<ExtractedTextSegment>> ExtractSegmentsAsync(byte[] fileContent, string fileExtension);
    Task<List<DocumentChunkDto>> ChunkTextAsync(string text, int chunkSize, int overlap, bool preserveTables = true);
    Task<List<DocumentChunkDto>> ChunkSegmentsAsync(
        IReadOnlyList<ExtractedTextSegment> segments,
        int chunkSize,
        int overlap,
        bool preserveTables = true);
    bool IsScannedPdf(byte[] fileContent);
}
