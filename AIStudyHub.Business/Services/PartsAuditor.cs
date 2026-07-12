using AIStudyHub.Business.DTOs.Questions;

namespace AIStudyHub.Business.Services;

public static class PartsAuditor
{
    /// <summary>
    /// Validates that a question follows PARTS (Plausible, Accurate, Relevant,
    /// Translated, Sourced) quality criteria:
    /// - Exactly 4 options
    /// - No "all of the above", "none of the above", or trick answer patterns
    /// - At least 3 distractor explanations
    /// </summary>
    public static bool IsValid(QuestionDto q)
    {
        if (q.Options.Count != 4) return false;
        foreach (var o in q.Options)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    o.Text,
                    @"^(Tất cả|None of the above|Cả A, B, C|All of the above|None)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return false;
        }
        var explanations = q.DistractorExplanations ?? new List<string>();
        return explanations.Count >= 3;
    }
}
