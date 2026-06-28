using AIStudyHub.Business.Services;
using Xunit;

namespace AIStudyHub.Tests;

public class GamificationServiceLevelTests
{
    [Fact]
    public void Level1_AtZeroXp()
    {
        Assert.Equal(1, GamificationService.ComputeLevel(0));
    }

    [Fact]
    public void Level2_At99Xp_Stays1_At100Xp_Becomes2()
    {
        Assert.Equal(1, GamificationService.ComputeLevel(99));
        Assert.Equal(2, GamificationService.ComputeLevel(100));
    }

    [Fact]
    public void Level5_At1000Xp()
    {
        Assert.Equal(5, GamificationService.ComputeLevel(1000));
    }

    [Fact]
    public void LevelCapsAtDefinedMax()
    {
        Assert.Equal(11, GamificationService.ComputeLevel(999_999));
    }

    [Fact]
    public void XpToNextLevel_FromLevel1_AtZero()
    {
        Assert.Equal(100, GamificationService.XpToNextLevel(1, 0));
    }

    [Fact]
    public void XpToNextLevel_AtMaxLevel_ReturnsZero()
    {
        Assert.Equal(0, GamificationService.XpToNextLevel(11, 99999));
    }
}