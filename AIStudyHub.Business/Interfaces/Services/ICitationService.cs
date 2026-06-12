using AIStudyHub.Business.DTOs.Rag;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ICitationService
{
    string FormatAnswerWithCitations(string answer, List<ReferenceDto> references);
    List<CitationDto> CreateCitations(List<ReferenceDto> references);
    List<ReferenceDto> CreateReferences(List<ChunkDto> chunks, Dictionary<Guid, string> documentTitles);
}
