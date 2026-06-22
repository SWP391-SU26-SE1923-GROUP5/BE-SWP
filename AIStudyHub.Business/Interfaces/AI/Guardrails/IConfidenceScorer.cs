namespace AIStudyHub.Business.Interfaces.AI.Guardrails;

public interface IConfidenceScorer
{
    double Score(string answer, AIStudyHub.Business.AI.Guardrails.GroundingResult grounding, bool isFaithful);
}
