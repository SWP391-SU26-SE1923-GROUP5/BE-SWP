using AIStudyHub.Business.AI.Guardrails;
using AIStudyHub.Business.Interfaces.AI.Search;

namespace AIStudyHub.Business.Interfaces.AI.Guardrails;

public interface IGroundingVerifier
{
    Task<GroundingResult> VerifyAsync(string answer, IEnumerable<SearchResult> sources);
}