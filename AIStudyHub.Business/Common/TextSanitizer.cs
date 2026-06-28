using System.Text;
using System.Text.RegularExpressions;

namespace AIStudyHub.Business.Common;

public static class TextSanitizer
{
    public static string FixMojibake(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (!input.Contains("Ã") && !input.Contains("Ä") && !input.Contains("áº"))
            return input;
        try { var latin1 = Encoding.GetEncoding("ISO-8859-1"); var bytes = latin1.GetBytes(input); var fixed_ = Encoding.UTF8.GetString(bytes); if (fixed_.Length < input.Length && !fixed_.Contains('\uFFFD')) return fixed_; } catch { }
        return input;
    }

    public static string CleanBracketedReferences(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return Regex.Replace(input, @"\s*\[[^\]]+\]", "").Trim();
    }
}
