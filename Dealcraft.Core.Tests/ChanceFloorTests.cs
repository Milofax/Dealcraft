using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The one number the player sets about negotiating. It is a floor and not a
/// target: <c>ConfidenceSearch.Best</c> picks the rung that earns most and this
/// only refuses that winner, so the search is never overridden — see
/// <c>CounterofferGateTests</c> for the refusing itself.
/// </summary>
public class ChanceFloorTests
{
    /// <summary>
    /// The ends the drawing gives it, and the bottom is not a new rule: the
    /// confidence ladder already starts at a half, because below that a counter
    /// is a coin toss with money already on the table.
    /// </summary>
    [Fact]
    public void The_control_runs_from_a_half_to_certainty_in_twentieths()
    {
        Assert.Equal(0.5f, ChanceFloor.Lowest);
        Assert.Equal(1f, ChanceFloor.Highest);
        Assert.Equal(0.05f, ChanceFloor.Step);
    }

    /// <summary>
    /// 50 and 100 as the player types them, which is the unit the row is drawn
    /// in and the unit the field takes back.
    /// </summary>
    [Fact]
    public void The_ends_are_fifty_and_a_hundred_as_the_player_types_them()
    {
        Assert.Equal(50f, ChanceFloor.LowestTyped);
        Assert.Equal(100f, ChanceFloor.HighestTyped);
    }

    /// <summary>
    /// A fresh install starts at 90%, and it starts on a rung — a default
    /// between two rungs would move the first time either button was pressed.
    /// </summary>
    [Fact]
    public void A_fresh_install_starts_at_ninety_per_cent_and_on_a_rung()
    {
        float fresh = new AdvisorSettings().AcceptanceProbabilityThreshold;

        Assert.Equal("90%", ChanceFloor.Spelled(fresh));
        Assert.InRange(fresh, ChanceFloor.Lowest, ChanceFloor.Highest);
        Assert.Equal("0.9", AutomationChoice.Range(
            fresh - ChanceFloor.Step, ChanceFloor.Lowest, ChanceFloor.Highest, ChanceFloor.Step).Next);
    }

    /// <summary>
    /// Spelling does not clamp either. A figure outside the control's own range
    /// — which only a hand-edited file produces — is drawn as what it is, so
    /// that the refusal a player reads in the debug file names the number the
    /// file actually holds.
    /// </summary>
    [Fact]
    public void A_figure_outside_the_range_is_spelled_as_what_it_is()
    {
        Assert.Equal("101%", ChanceFloor.Spelled(1.01f));
        Assert.Equal("20%", ChanceFloor.Spelled(0.2f));
    }

    /// <summary>
    /// The key is the one the preferences file has always had. It is the same
    /// setting — a threshold the search settled for, now a threshold the
    /// search's answer is held to — so renaming it would leave an orphan in
    /// every file on disk for a change of meaning the description states.
    /// </summary>
    [Fact]
    public void The_key_is_the_one_the_file_already_holds()
    {
        Assert.Equal("AcceptanceProbabilityThreshold", ChanceFloor.Key);
    }
}
