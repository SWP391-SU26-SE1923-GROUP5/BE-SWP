using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIStudyHub.Business.AI.Generators.Common;

public abstract class BatchGeneratorBase<T>
{
    protected abstract Task<List<T>> RunBatchWithRetryAsync(
        int batchSize,
        string avoidBlock,
        CancellationToken cancellationToken);

    protected abstract string NormalizeForDedup(T item);

    public static string ExtractBalancedObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return text;
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0) return text.Substring(start, i - start + 1);
            }
        }
        return text;
    }

    public static string ExtractBalancedArray(string text)
    {
        var start = text.IndexOf('[');
        if (start < 0) return text;
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']')
            {
                depth--;
                if (depth == 0) return text.Substring(start, i - start + 1);
            }
        }
        return text;
    }

    public static string NormalizeString(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        return Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]", "");
    }

    public static string ExtractBalanced(string text, char open, char close)
    {
        var startIdx = text.IndexOf(open);
        if (startIdx < 0) return text;

        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = startIdx; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == open) depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0) return text.Substring(startIdx, i - startIdx + 1);
            }
        }
        return text;
    }
}
