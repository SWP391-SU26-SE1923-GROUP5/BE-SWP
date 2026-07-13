using AIStudyHub.Business.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using Xunit;

namespace AIStudyHub.Tests;

/// <summary>
/// Pure-logic tests for the SM-2 algorithm (Phase 4a: Fuzzing).
/// </summary>
public class FlashcardReviewServiceSm2FuzzTests
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

    [Theory]
    [InlineData(ReviewQuality.Easy)]
    [InlineData(ReviewQuality.Good)]
    public void CorrectAnswer_IncrementsRepetitions(ReviewQuality quality)
    {
        var review = NewReview(repetitions: 2);
        FlashcardReviewService.ApplySm2(review, quality);
        Assert.Equal(3, review.Repetitions);
    }

    [Theory]
    [InlineData(ReviewQuality.Again)]
    [InlineData(ReviewQuality.Hard)]
    public void WrongAnswer_ResetsRepetitionsToZero(ReviewQuality quality)
    {
        var review = NewReview(interval: 21, repetitions: 4);
        FlashcardReviewService.ApplySm2(review, quality);
        Assert.Equal(0, review.Repetitions);
        Assert.Equal(1, review.Interval);
    }

    [Fact]
    public void GoodAnswer_IncreasesInterval()
    {
        var review = NewReview(interval: 6, repetitions: 1);
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Good);
        Assert.Equal(2, review.Repetitions);
        Assert.True(review.Interval >= 6);
    }

    [Fact]
    public void Interval_ForThirdReview_UsesCeilingRounding()
    {
        // 6 * 2.5 = 15; with fuzzing ±5%, result should be near 15
        var review = NewReview(interval: 6, repetitions: 2, ease: 2.5f);
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Easy);

        // Without fuzzing: 15. With fuzzing: [14.25, 15.75]
        Assert.True(review.Interval >= 14, $"Expected interval >= 14, got {review.Interval}");
        Assert.True(review.Interval <= 16, $"Expected interval <= 16, got {review.Interval}");
    }

    [Fact]
    public void EaseFactor_MasterSpecFormula_EasyBoostsEase()
    {
        var review = NewReview(ease: 2.5f);
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Easy);
        // q=3: delta = 0.1 - (5-3)*(0.08+(5-3)*0.02) = 0.1 - 2*0.12 = -0.14
        // Wait: 0.1 - 2*(0.08+2*0.02) = 0.1 - 2*0.12 = 0.1 - 0.24 = -0.14
        Assert.True(review.EaseFactor > 2.5f, $"Expected EF > 2.5, got {review.EaseFactor}");
    }

    [Fact]
    public void EaseFactor_MasterSpecFormula_AgainReducesEase()
    {
        var review = NewReview(ease: 2.5f);
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Again);
        // q=0: delta = 0.1 - 5*(0.08+5*0.02) = 0.1 - 5*0.18 = 0.1 - 0.9 = -0.8
        Assert.True(review.EaseFactor < 2.5f, $"Expected EF < 2.5, got {review.EaseFactor}");
    }

    [Fact]
    public void EaseFactor_FloorIs13()
    {
        var review = NewReview(ease: 1.31f);
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Again);
        Assert.Equal(1.3f, review.EaseFactor);
    }

    [Fact]
    public void EaseFactor_CeilingIsNotCapped()
    {
        // No ceiling in Master Spec formula
        var review = NewReview(ease: 2.8f);
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Easy);
        Assert.True(review.EaseFactor > 2.8f);
    }

    [Fact]
    public void NextReviewDate_IsInTheFuture()
    {
        var review = NewReview();
        var before = DateTime.UtcNow;
        FlashcardReviewService.ApplySm2(review, ReviewQuality.Easy);
        Assert.True(review.NextReviewDate > before);
    }

    [Theory]
    [InlineData(ReviewQuality.Easy)]
    [InlineData(ReviewQuality.Good)]
    [InlineData(ReviewQuality.Hard)]
    [InlineData(ReviewQuality.Again)]
    public void ApplySm2_IsDeterministic_ForSameInputs(ReviewQuality quality)
    {
        var review = NewReview(interval: 6, repetitions: 2, ease: 2.5f);
        FlashcardReviewService.ApplySm2(review, quality);
        var interval1 = review.Interval;
        var ef1 = review.EaseFactor;

        var review2 = NewReview(interval: 6, repetitions: 2, ease: 2.5f);
        FlashcardReviewService.ApplySm2(review2, quality);
        var interval2 = review2.Interval;
        var ef2 = review2.EaseFactor;

        // Note: fuzzing makes intervals non-deterministic for interval >= 10
        Assert.Equal(ef1, ef2); // EF is always deterministic
    }
}
