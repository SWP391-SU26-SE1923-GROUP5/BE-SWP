using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.Services;

namespace AIStudyHub.Business.Services;

public sealed class CitationService : ICitationService
{
    public string FormatAnswerWithCitations(string answer, List<ReferenceDto> references)
    {
        if (references.Count == 0)
            return answer;

        var citationMap = new Dictionary<int, int>();
        for (var i = 0; i < references.Count; i++)
            citationMap[i + 1] = i;

        foreach (var (refIndex, _) in references.Select((r, i) => (i + 1, r)))
        {
            var patterns = new[]
            {
                $"[{refIndex}]",
                $"[{refIndex.ToString().TrimStart('0')}]"
            };

            foreach (var pattern in patterns)
            {
                if (answer.Contains(pattern))
                {
                    answer = answer.Replace(pattern, $"[{refIndex}]");
                }
            }
        }

        return answer;
    }

    public List<CitationDto> CreateCitations(List<ReferenceDto> references)
    {
        return references.Select((ref_, index) => new CitationDto(
            index + 1,
            ref_.DocumentTitle,
            TruncateText(ref_.ChunkExcerpt, 150)
        )).ToList();
    }

    public List<ReferenceDto> CreateReferences(List<ChunkDto> chunks, Dictionary<Guid, string> documentTitles)
    {
        return chunks.Select((chunk, index) => new ReferenceDto(
            index + 1,
            chunk.DocumentId,
            documentTitles.GetValueOrDefault(chunk.DocumentId, "Unknown Document"),
            $"Chunk {chunk.OrderIndex + 1}",
            TruncateText(chunk.Content, 200)
        )).ToList();
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        var truncated = text[..(maxLength - 3)];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength / 2)
            truncated = truncated[..lastSpace];

        return truncated + "...";
    }
}
