using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class DealWindowSetTests
{
    [Fact]
    public void A_new_set_allows_nothing()
    {
        var windows = new DealWindowSet();

        Assert.Empty(windows.Allowed);
        Assert.True(windows.AllowsNothing);
    }

    [Fact]
    public void A_window_that_was_allowed_is_allowed()
    {
        var windows = new DealWindowSet();

        windows.Allow(DealWindow.Afternoon, true);

        Assert.True(windows.Allows(DealWindow.Afternoon));
        Assert.False(windows.Allows(DealWindow.Morning));
    }

    /// <summary>
    /// The headline difference from the original: four independent switches,
    /// not one integer. A player who never deals after dark turns two off and
    /// keeps the other two.
    /// </summary>
    [Fact]
    public void Each_of_the_four_windows_is_switched_independently()
    {
        var windows = new DealWindowSet();
        windows.Allow(DealWindow.Morning, true);
        windows.Allow(DealWindow.Afternoon, true);
        windows.Allow(DealWindow.Night, true);
        windows.Allow(DealWindow.LateNight, true);

        windows.Allow(DealWindow.Night, false);
        windows.Allow(DealWindow.LateNight, false);

        Assert.Equal(
            new[] { DealWindow.Morning, DealWindow.Afternoon },
            windows.Allowed);
    }

    [Fact]
    public void Allowed_windows_come_back_in_the_order_the_day_runs()
    {
        var windows = new DealWindowSet();
        windows.Allow(DealWindow.LateNight, true);
        windows.Allow(DealWindow.Morning, true);
        windows.Allow(DealWindow.Night, true);

        Assert.Equal(
            new[] { DealWindow.Morning, DealWindow.Night, DealWindow.LateNight },
            windows.Allowed);
    }

    [Fact]
    public void Allowing_the_same_window_twice_lists_it_once()
    {
        var windows = new DealWindowSet();
        windows.Allow(DealWindow.Morning, true);
        windows.Allow(DealWindow.Morning, true);

        Assert.Single(windows.Allowed);
    }

    [Fact]
    public void Turning_the_last_window_off_leaves_nothing_allowed()
    {
        var windows = new DealWindowSet();
        windows.Allow(DealWindow.Morning, true);

        windows.Allow(DealWindow.Morning, false);

        Assert.True(windows.AllowsNothing);
    }

    /// <summary>
    /// What a log line says the host allowed. Spelled the way a player reads
    /// the window in the game, not the way the enum spells it.
    /// </summary>
    [Fact]
    public void The_set_spells_itself_out_for_the_log()
    {
        var windows = new DealWindowSet();
        windows.Allow(DealWindow.Afternoon, true);
        windows.Allow(DealWindow.LateNight, true);

        Assert.Equal("Afternoon, Late Night", windows.ToString());
    }

    [Fact]
    public void An_empty_set_says_so_rather_than_reading_blank()
    {
        Assert.Equal("none", new DealWindowSet().ToString());
    }

    /// <summary>
    /// The four values must match Il2CppScheduleOne.Economy.EDealWindow, read
    /// off the 0.4.6f13 interop assemblies, because the adapter casts between
    /// them rather than translating. A test is the only place that fact can be
    /// stated without an Il2Cpp reference.
    /// </summary>
    [Fact]
    public void The_window_numbers_are_the_games_own()
    {
        Assert.Equal(0, (int)DealWindow.Morning);
        Assert.Equal(1, (int)DealWindow.Afternoon);
        Assert.Equal(2, (int)DealWindow.Night);
        Assert.Equal(3, (int)DealWindow.LateNight);
        Assert.Equal(4, DealWindowSet.All.Count());
    }
}
