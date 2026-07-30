using System.Globalization;
using System.Text;

namespace AIStudyHub.Business.AI.Orchestration;

public static class RagPromptContextBuilder
{
    public static string Build(IEnumerable<RagContextSource> contexts)
    {
        var context = new StringBuilder();
        foreach (var source in contexts)
        {
            context.AppendLine("--- DOCUMENT CONTEXT ---");
            context.AppendLine($"FILE_NAME: {source.Result.Source}");
            context.AppendLine($"PAGE_NUMBER: {source.PageNumber?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}");
            context.AppendLine("CONTENT:");
            context.AppendLine(source.Result.Content);
            context.AppendLine("--- END CONTEXT ---");
            context.AppendLine();
        }

        return context.ToString();
    }
}
