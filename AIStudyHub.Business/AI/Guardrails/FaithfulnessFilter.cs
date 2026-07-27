using System.Text.RegularExpressions;
using AIStudyHub.Business.Interfaces.AI.Guardrails;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.AI.Guardrails;

public class FaithfulnessFilter : IFaithfulnessFilter
{
    private readonly ILogger<FaithfulnessFilter> _logger;

    private static readonly string[] EvasionIndicators = [
        // English
        "i don't know", "i dont know", "cannot find", "can't find",
        "not mentioned in the provided", "not mentioned in the context",
        "i cannot answer", "i'm not able to answer",
        // Vietnamese
        "không tìm thấy", "không có trong tài liệu", "không được đề cập",
        "tài liệu không chứa", "không thể trả lời", "không có thông tin",
        "tôi không biết", "tôi không thể"
    ];

    public FaithfulnessFilter(ILogger<FaithfulnessFilter> logger)
    {
        _logger = logger;
    }

    public Task<bool> ValidateAsync(string answer, IEnumerable<string> sourceContents)
    {
        var context = string.Join(" ", sourceContents);
        var answerLower = answer.ToLowerInvariant();

        var hasContext = context.Length > 100;

        if (!hasContext)
            return Task.FromResult(true);

        // Check for evasive phrases - must appear as whole word/phrase, not buried inside other text
        foreach (var phrase in EvasionIndicators)
        {
            // Use word boundary check to avoid false positives
            // e.g. "cannot be automated" should NOT match "cannot find"
            var pattern = $@"(?<!\w){Regex.Escape(phrase)}(?!\w)";
            if (Regex.IsMatch(answerLower, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning(
                    "Faithfulness check failed: evasive phrase detected in answer (phrase=\"{Phrase}\")",
                    phrase);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }
}
