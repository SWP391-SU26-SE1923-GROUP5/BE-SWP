namespace AIStudyHub.Business.DTOs.AI;

public record TokenUsageResult(string Text, int InputTokens, int OutputTokens);

public record AiOperationResult<T>(T Result, int InputTokens, int OutputTokens);
