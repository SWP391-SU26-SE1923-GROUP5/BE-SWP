using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.AI.Search;  namespace AIStudyHub.Business.Interfaces.AI.Guardrails;  public interface IGroundingVerifier {     Task<AIStudyHub.Business.AI.Guardrails.GroundingResult> VerifyAsync(string answer, IEnumerable<SearchResult> sources); }
