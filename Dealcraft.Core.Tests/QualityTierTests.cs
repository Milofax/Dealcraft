using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The grade ladder's own ends. Trash is rank zero, which is why it needs
/// saying: a ladder that started at Poor would quietly leave the bottom grade
/// out of every delivery plan, and the plan would say the stock did not exist
/// rather than that it was not good enough.
/// </summary>
public class QualityTierTests
{
    [Theory]
    [InlineData(QualityTier.Trash, "Trash")]
    [InlineData(QualityTier.Poor, "Poor")]
    [InlineData(QualityTier.Standard, "Standard")]
    [InlineData(QualityTier.Premium, "Premium")]
    [InlineData(QualityTier.Heavenly, "Heavenly")]
    public void Every_grade_the_game_has_is_known_and_named(int quality, string name)
    {
        Assert.True(QualityTier.IsKnown(quality), $"{name} is a grade the game has");
        Assert.Equal(name, QualityTier.Name(quality));
    }

    /// <summary>
    /// A grade outside the ladder is left out of every plan rather than ranked
    /// against the others on a guess, and it is still spelled in a way a player
    /// could report.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void A_grade_outside_the_ladder_is_not_known(int quality)
    {
        Assert.False(QualityTier.IsKnown(quality));
        Assert.Contains(quality.ToString(), QualityTier.Name(quality));
    }

    [Fact]
    public void The_ladder_runs_from_Trash_to_Heavenly()
    {
        Assert.Equal(QualityTier.Trash, QualityTier.Lowest);
        Assert.Equal(QualityTier.Heavenly, QualityTier.Highest);
    }
}
