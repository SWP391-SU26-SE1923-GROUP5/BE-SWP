using AIStudyHub.Business.Interfaces.AI.Guardrails;
using AIStudyHub.Business.Configuration;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.AI.Guardrails;

public class ConfidenceScorer : IConfidenceScorer
{
    private readonly GuardrailsOptions _options;

    public ConfidenceScorer(IOptions<GuardrailsOptions> options)
    {
        _options = options.Value;
    }

    public double Score(string answer, GroundingResult grounding, bool isFaithful)
    {
        var score = grounding.Score;

        if (!isFaithful)
        {
            score *= 0.5;
        }

        if (answer.Length < 50)
        {
            score *= 0.8;
        }

        if (grounding.Score > _options.GroundingThreshold)
        {
            score += 0.1;
        }

        return Math.Clamp(score, 0, 1);
    }
}
