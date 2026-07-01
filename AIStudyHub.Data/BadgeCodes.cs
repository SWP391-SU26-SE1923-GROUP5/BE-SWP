namespace AIStudyHub.Data;

/// <summary>
/// Stable string identifiers for the five Master Spec badges.
/// Centralised so service code never hard-codes a typo.
/// </summary>
public static class BadgeCodes
{
    public const string Streak7D = "STREAK_7D";
    public const string Cards500 = "CARDS_500";
    public const string MasteryMath = "MASTERY_MATH";
    public const string Sharpshooter = "SHARPSHOOTER";
    public const string Bookworm = "BOOKWORM";

    public static class Categories
    {
        public const string Streak = "Streak";
        public const string Volume = "Volume";
        public const string Mastery = "Mastery";
        public const string Accuracy = "Accuracy";
        public const string Content = "Content";
    }
}
