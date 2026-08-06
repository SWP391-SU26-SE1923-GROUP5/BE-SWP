using System.Globalization;
using System.Text;

namespace AIStudyHub.Business.AI.Orchestration;

public sealed record RagPromptContext(
    string Text,
    IReadOnlyDictionary<string, RagContextSource> SourcesById);

public static class RagPromptContextBuilder
{
    public static RagPromptContext Build(
        IReadOnlyList<RagContextSource> contexts)
    {
        var text = new StringBuilder();
        var sourcesById = new Dictionary<string, RagContextSource>(
            StringComparer.Ordinal);

        for (var index = 0; index < contexts.Count; index++)
        {
            var sourceId = $"S{index + 1:D3}";
            var source = contexts[index];
            sourcesById.Add(sourceId, source);

            text.AppendLine("--- DOCUMENT CONTEXT ---");
            text.AppendLine($"SOURCE_ID: {sourceId}");
            text.AppendLine($"FILE_NAME: {source.Result.Source}");
            text.AppendLine(
                $"PAGE_NUMBER: {source.PageNumber?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}");
            text.AppendLine("CONTENT:");
            text.AppendLine(source.Result.Content);
            text.AppendLine("--- END CONTEXT ---");
            text.AppendLine();
        }

        return new RagPromptContext(text.ToString(), sourcesById);
    }
}
