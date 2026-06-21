using AIStudyHub.Business.Interfaces.AI.Guardrails;
using AIStudyHub.Business.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.AI.Guardrails;

public class FaithfulnessFilter : IFaithfulnessFilter
{
    private readonly GuardrailsOptions _options;
    private readonly ILogger<FaithfulnessFilter> _logger;

    public FaithfulnessFilter(IOptions<GuardrailsOptions> options, ILogger<FaithfulnessFilter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> ValidateAsync(string answer, IEnumerable<string> sourceContents)
    {
        var context = string.Join(" ", sourceContents);
        var answerLower = answer.ToLowerInvariant();
        
        var hasContext = context.Length > 100;
        var isEvasive = answerLower.Contains("cannot find") || 
                        answerLower.Contains("i don't know") ||
                        answerLower.Contains("not mentioned");

        if (hasContext && isEvasive)
        {
            _logger.LogWarning("Faithfulness check failed: evasive answer despite available context");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
