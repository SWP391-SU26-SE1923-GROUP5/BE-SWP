namespace AIStudyHub.Data.Enums;

public enum RecommendationType
{
    WeakSubject = 1,    // user mastery < 60% for a subject
    LeechCard = 2,       // flashcard with lapses >= 4
    Stagnation = 3,     // no study activity in 7 days
    StreakAtRisk = 4    // streak about to break
}
