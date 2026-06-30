using System.Text.Json;

namespace AIStudyHub.Business.AI.Generators.Common;

public static class BatchParsingHelpers
{
    public static JsonElement WrapSingleObject(JsonElement obj)
    {
        using var ms = new MemoryStream();
        using var w = new Utf8JsonWriter(ms);
        w.WriteStartArray();
        w.WriteRawValue(obj.GetRawText(), skipInputValidation: true);
        w.WriteEndArray();
        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    public static List<T> ParseArrayStreaming<T>(
        string rawText,
        Func<JsonElement, IEnumerable<T>> extractor)
    {
        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            rawText, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");

        var result = new List<T>();
        var i = 0;

        while (i < sanitized.Length)
        {
            while (i < sanitized.Length
                && (char.IsWhiteSpace(sanitized[i])
                    || sanitized[i] == ','
                    || sanitized[i] == '['
                    || sanitized[i] == ']'))
            {
                i++;
            }

            if (i >= sanitized.Length) break;

            if (sanitized[i] != '{') { i++; continue; }

            var objStart = i;
            var depth = 0;
            var inString = false;
            var escape = false;
            var found = false;

            for (; i < sanitized.Length; i++)
            {
                var c = sanitized[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) { found = true; i++; break; }
                }
            }

            if (!found) break;

            var slice = sanitized.Substring(objStart, i - objStart);
            try
            {
                using var doc = JsonDocument.Parse(slice,
                    new JsonDocumentOptions { AllowTrailingCommas = true });
                result.AddRange(extractor(WrapSingleObject(doc.RootElement.Clone())));
            }
            catch (JsonException)
            {
                // Skip broken element
            }
        }

        return result;
    }
}
