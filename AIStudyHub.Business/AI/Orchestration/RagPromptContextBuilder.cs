using System.Text;
using AIStudyHub.Business.Interfaces.AI.Search;

namespace AIStudyHub.Business.AI.Orchestration;

public static class RagPromptContextBuilder
{
    public static string Build(IEnumerable<SearchResult> results)
    {
        var context = new StringBuilder();
        foreach (var result in results)
        {
            context.AppendLine("--- SOURCE ---");
            context.AppendLine($"DOCUMENT: {result.Source}");
            if (result.Metadata.TryGetValue("pageNumber", out var page))
            {
                context.AppendLine($"PDF_PHYSICAL_PAGE: {page}");
                context.AppendLine($"AUTHORITATIVE_CITATION_PAGE: {page}");
            }
            else
            {
                context.AppendLine("PAGE_CITATION_AVAILABLE: false");
            }
            context.AppendLine("CONTENT:");
            context.AppendLine(result.Content);
            context.AppendLine("--- END SOURCE ---");
            context.AppendLine();
        }

        return context.ToString();
    }
}
