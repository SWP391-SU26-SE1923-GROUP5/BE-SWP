using System.Collections.Generic;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Quizzes;
public sealed record CreateQuizRequestViaAIDto(int numberOfQuestions);
public sealed record AiGeneratedAnswerDto(string SelectedOption, bool IsCorrect);

public sealed record AiGeneratedQuestionDto(
    string QuestionTitle,
    QuestionType QuestionType,
    int Position,
    IReadOnlyList<AiGeneratedAnswerDto> Answers
);

public sealed record AiGeneratedQuizResponseDto(string QuizTitle, IReadOnlyList<AiGeneratedQuestionDto> Questions);
