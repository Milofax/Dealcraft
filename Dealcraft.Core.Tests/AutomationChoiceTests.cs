using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// What activating an Automation row writes back. The spec's rule is that "a
/// change made there writes straight back", so the value a row offers next has
/// to be spelled exactly as the preferences file spells it — the app is a
/// reading of the file, and a row that wrote "0,75" where the file says "0.75"
/// would be a second store by the back door.
/// </summary>
public class AutomationChoiceTests
{
    [Fact]
    public void A_switch_that_is_off_offers_to_go_on()
    {
        AutomationChoice choice = AutomationChoice.Switch(on: false);

        Assert.True(choice.CanChange);
        Assert.Equal("On", choice.Next);
    }

    [Fact]
    public void A_switch_that_is_on_offers_to_go_off()
    {
        Assert.Equal("Off", AutomationChoice.Switch(on: true).Next);
    }

    [Fact]
    public void A_number_steps_up_to_the_next_rung()
    {
        Assert.Equal("60", AutomationChoice.Ladder(40f, 0f, 20f, 40f, 60f).Next);
    }

    /// <summary>
    /// Wrapping is what makes one button enough for a number: a player who
    /// stepped past the value they wanted goes round rather than being stuck at
    /// the top with nowhere to go.
    /// </summary>
    [Fact]
    public void A_number_at_the_top_of_the_ladder_wraps_to_the_bottom()
    {
        Assert.Equal("0", AutomationChoice.Ladder(60f, 0f, 20f, 40f, 60f).Next);
    }

    [Fact]
    public void A_number_above_the_whole_ladder_wraps_to_the_bottom()
    {
        Assert.Equal("0", AutomationChoice.Ladder(250f, 0f, 20f, 40f, 60f).Next);
    }

    /// <summary>
    /// A value typed into the file by hand is not going to be one of the rungs.
    /// Stepping up from it lands on the first rung above it, so the app never
    /// silently throws the player's own number away by restarting at the bottom.
    /// </summary>
    [Fact]
    public void A_hand_edited_number_steps_up_to_the_first_rung_above_it()
    {
        Assert.Equal("40", AutomationChoice.Ladder(37f, 0f, 20f, 40f, 60f).Next);
    }

    /// <summary>
    /// The same spelling the row shows and the file holds. "0.75", not "0.7500"
    /// and not a locale's "0,75".
    /// </summary>
    [Fact]
    public void A_fraction_is_spelled_the_way_the_file_spells_it()
    {
        Assert.Equal("0.75", AutomationChoice.Ladder(0.7f, 0.7f, 0.75f, 0.8f).Next);
    }

    [Fact]
    public void A_setting_the_app_cannot_change_offers_nothing()
    {
        AutomationChoice choice = AutomationChoice.FileOnly;

        Assert.False(choice.CanChange);
        Assert.Equal(string.Empty, choice.Next);
        Assert.Equal(string.Empty, choice.Previous);
    }

    // --- a range, which has two directions and blunt ends ---------------------

    [Fact]
    public void A_range_steps_both_ways_by_its_own_step()
    {
        AutomationChoice choice = Chance(0.9f);

        Assert.True(choice.CanChange);
        Assert.Equal("0.95", choice.Next);
        Assert.Equal("0.85", choice.Previous);
    }

    /// <summary>
    /// Both ends are blunt, and that is the difference between a range and a
    /// ladder. Wrapping from 100% to 50% on one press is the opposite of what a
    /// player standing at the top asked for.
    /// </summary>
    [Fact]
    public void A_range_at_the_top_offers_nothing_above_it()
    {
        AutomationChoice choice = Chance(ChanceFloor.Highest);

        Assert.Equal(string.Empty, choice.Next);
        Assert.Equal("0.95", choice.Previous);
        Assert.True(choice.CanChange);
    }

    [Fact]
    public void A_range_at_the_bottom_offers_nothing_below_it()
    {
        AutomationChoice choice = Chance(ChanceFloor.Lowest);

        Assert.Equal("0.55", choice.Next);
        Assert.Equal(string.Empty, choice.Previous);
    }

    /// <summary>
    /// Both ends are reachable from the rung beside them, so the control can
    /// actually be put at 50 and at 100 rather than only near them.
    /// </summary>
    [Fact]
    public void Both_ends_of_the_range_are_reachable()
    {
        Assert.Equal("1", Chance(0.95f).Next);
        Assert.Equal("0.5", Chance(0.55f).Previous);
    }

    /// <summary>
    /// A value edited into the file by hand is not going to be on the ladder.
    /// Either direction lands on the nearest rung that way, so the player's own
    /// number is stepped off rather than thrown away.
    /// </summary>
    [Fact]
    public void A_hand_edited_number_steps_onto_the_ladder_in_either_direction()
    {
        AutomationChoice choice = Chance(0.77f);

        Assert.Equal("0.8", choice.Next);
        Assert.Equal("0.75", choice.Previous);
    }

    /// <summary>
    /// And one edited outside the range altogether steps back into it. Blunt
    /// ends must not mean a player who typed 0.2 into the file is stuck there
    /// with both buttons dead.
    /// </summary>
    [Fact]
    public void A_number_outside_the_range_steps_back_into_it()
    {
        Assert.Equal("0.5", Chance(0.2f).Next);
        Assert.Equal(string.Empty, Chance(0.2f).Previous);

        Assert.Equal("1", Chance(1.4f).Previous);
        Assert.Equal(string.Empty, Chance(1.4f).Next);
    }

    private static AutomationChoice Chance(float current) => AutomationChoice.Range(
        current, ChanceFloor.Lowest, ChanceFloor.Highest, ChanceFloor.Step);
}
