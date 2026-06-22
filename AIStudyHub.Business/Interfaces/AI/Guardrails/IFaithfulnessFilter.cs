namespace AIStudyHub.Business.Interfaces.AI.Guardrails;

public interface IFaithfulnessFilter
{
    Task<bool> ValidateAsync(string answer, IEnumerable<string> sourceContents);
}
