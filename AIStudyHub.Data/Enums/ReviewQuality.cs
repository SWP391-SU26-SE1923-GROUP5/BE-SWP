namespace AIStudyHub.Data.Enums;

/// <summary>
/// SM-2 quality grade for a flashcard review.
/// Values map to the standard Anki scale (0 = blackout, 5 = perfect).
/// We collapse to 4 levels that match the message.txt spec (Easy / Good / Hard / Again).
/// </summary>
public enum ReviewQuality
{
    /// <summary>Complete blackout or wrong answer. Resets repetitions.</summary>
    Again = 0,

    /// <summary>Incorrect but remembered upon seeing the answer.</summary>
    Hard = 1,

    /// <summary>Correct with serious difficulty.</summary>
    Good = 2,

    /// <summary>Correct with hesitation (default acceptable response).</summary>
    Easy = 3
}
