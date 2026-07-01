using AIStudyHub.Business.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using Xunit;

namespace AIStudyHub.Tests;

/// <summary>
/// Pure-logic tests for the SM-2 algorithm in FlashcardReviewService.ApplySm2.
/// We exercise the internal static method so the test does not need a database.
/// </summary>
public class FlashcardReviewServiceSm2Tests
{
    private static FlashcardReview NewReview(int interval = 1, int repetitions = 0, float ease = 2.5f)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FlashcardId = Guid.NewGuid(),
            Interval = interval,
            Repetitions = repetitions,
            EaseFactor = ease,
            NextReviewDate = DateTime.UtcNow
        };

    [Fact]
    public void Easy_OnFirstReview_SetsIntervalTo1()
    {
        var review = NewReview();
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Easy);

        Assert.Equal(1, review.Repetitions);
        Assert.Equal(1, review.Interval);
        Assert.True(review.EaseFactor > 2.5f, "Ease should increase after a perfect review.");
        Assert.True(review.NextReviewDate > DateTime.UtcNow);
    }

    [Fact]
    public void Easy_OnSecondReview_SetsIntervalTo6()
    {
        var review = NewReview(repetitions: 1);
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Easy);

        Assert.Equal(2, review.Repetitions);
        Assert.Equal(6, review.Interval);
    }

    [Fact]
    public void Easy_AfterTwoReviews_MultipliesIntervalByEaseFactor()
    {
        var review = NewReview(interval: 6, repetitions: 2, ease: 2.5f);
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Easy);

        Assert.Equal(3, review.Repetitions);
        Assert.Equal(15, review.Interval); // round(6 * 2.5) = 15
    }

    [Theory]
    [InlineData(ReviewQuality.Again)]
    [InlineData(ReviewQuality.Hard)]
    public void WrongAnswer_ResetsRepetitionsAndInterval(ReviewQuality quality)
    {
        var review = NewReview(interval: 21, repetitions: 4, ease: 2.6f);
        FlashcardReviewService.ApplySm2(review, quality);

        Assert.Equal(0, review.Repetitions);
        Assert.Equal(1, review.Interval);
    }

    [Fact]
    public void EaseFactor_NeverDropsBelow13()
    {
        var review = NewReview(ease: 1.31f);
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Again);

        Assert.Equal(1.3f, review.EaseFactor, 2);
    }
}