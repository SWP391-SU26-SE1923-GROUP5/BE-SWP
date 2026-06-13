using AIStudyHub.Business.DTOs.Rag;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IDocumentProcessingService
{
    Task<string> ExtractTextAsync(byte[] fileContent, string fileExtension);
    Task<List<string>> ChunkTextAsync(string text, int chunkSize, int overlap);
}
