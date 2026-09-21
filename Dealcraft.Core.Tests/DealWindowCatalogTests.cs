using System;
using System.Collections.Generic;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class DealWindowCatalogTests
{
    /// <summary>
    /// The hours the game reported in 0.4.6f13. They are written out here
    /// because a test needs a reading to work from, not because the mod knows
    /// them: the adapter reads DealWindowInfo at runtime and a patch that moves
    /// a window moves what the app shows.
    /// </summary>
    private static IReadOnlyList<DealWindowHours> AsTheGameReportedThem() => new[]
    {
        new DealWindowHours(DealWindow.Morning, 600, 1200),
        new DealWindowHours(DealWindow.Afternoon, 1200, 1800),
        new DealWindowHours(DealWindow.Night, 1800, 2400),
        new DealWindowHours(DealWindow.LateNight, 0, 600),
    };

    /// <summary>
    /// The row identities are MelonPreferences entry names, so a value edited
    /// in the file and a row read in the app are demonstrably the same setting.
    /// These literals are the config file's contract.
    /// </summary>
    [Fact]
    public void Every_window_row_is_named_after_its_preferences_entry()
    {
        var windows = new DealWindowSet();

        Assert.Equal(
            new[]
            {
                "AllowMorningWindow",
                "AllowAfternoonWindow",
                "AllowNightWindow",
                "AllowLateNightWindow",
            },
            DealWindowCatalog.Describe(windows, AsTheGameReportedThem()).Select(row => row.Key));
    }

    /// <summary>
    /// A player reading "Afternoon" must be able to see which hours that is.
    /// </summary>
    [Fact]
    public void Each_row_shows_the_windows_real_clock_times()
    {
        var windows = new DealWindowSet();

        AutomationSetting afternoon = DealWindowCatalog.Describe(windows, AsTheGameReportedThem())
            .Single(row => row.Key == "AllowAfternoonWindow");

        Assert.Equal("Afternoon", afternoon.Title);
        Assert.Contains("12:00", afternoon.Summary);
        Assert.Contains("18:00", afternoon.Summary);
    }

    [Fact]
    public void Each_row_reports_whether_its_window_is_allowed()
    {
        var windows = new DealWindowSet();
        windows.Allow(DealWindow.Morning, true);
        windows.Allow(DealWindow.LateNight, true);

        Assert.Equal(
            new[] { "On", "Off", "Off", "On" },
            WindowRows(DealWindowCatalog.Describe(windows, AsTheGameReportedThem()))
                .Select(row => row.Value));
    }

    /// <summary>
    /// Scheduling is the host's to do, like every other automation row, and the
    /// app must not invite a non-host to turn it.
    /// </summary>
    [Fact]
    public void Every_window_row_is_the_hosts()
    {
        var windows = new DealWindowSet();

        Assert.All(
            DealWindowCatalog.Describe(windows, AsTheGameReportedThem()),
            row => Assert.True(row.HostOnly));
    }

    /// <summary>
    /// The clock times are the whole of what a window row has to say, and they
    /// are the game's reading rather than anything written down here.
    /// </summary>
    [Fact]
    public void Every_window_row_says_its_hours_and_nothing_more()
    {
        var windows = new DealWindowSet();

        Assert.All(
            WindowRows(DealWindowCatalog.Describe(windows, AsTheGameReportedThem())),
            row => Assert.Matches(@"^\d?\d:\d\d to \d?\d:\d\d$", row.Summary));
    }

    /// <summary>
    /// The adapter reads the hours off the game, and the game may not be there
    /// — no save loaded, or a read that threw. The rows must still list the
    /// four windows and their switches; only the hours go missing.
    /// </summary>
    [Fact]
    public void Without_hours_from_the_game_the_rows_still_list_the_four_windows()
    {
        var windows = new DealWindowSet();

        IReadOnlyList<AutomationSetting> rows = WindowRows(
            DealWindowCatalog.Describe(windows, Array.Empty<DealWindowHours>()));

        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.Contains(
            "not reported", row.Summary, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// All four off is a real answer and the page does not yet say what it
    /// means: the block is four empty boxes while the other three say
    /// <c>Manual (game's default)</c> in words.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There used to be a <c>NothingAllowed</c> constant here, and a test that
    /// asserted it contained a substring of itself. No production code ever read
    /// it, and the form's <c>AcceptedSchedule</c> builds four toggles and nothing
    /// else — so a green test and a user-facing sentence in the tree claimed a
    /// gap was closed that is open. Both are deleted.
    /// </para>
    /// <para>
    /// The gap itself is <c>issues/45-the-schedule-says-whether-as-well-as-when.md</c>,
    /// which gives the block the same two answers as the other three. This test
    /// holds the state until then: the block is the four windows, and nothing
    /// claims otherwise.
    /// </para>
    /// </remarks>
    [Fact]
    public void Nothing_in_the_catalogue_claims_to_say_what_all_four_off_means()
    {
        IReadOnlyList<AutomationSetting> rows =
            DealWindowCatalog.Describe(new DealWindowSet(), Array.Empty<DealWindowHours>());

        Assert.Equal(4, rows.Count);
        Assert.DoesNotContain(rows, row => row.Summary.Contains(
            "nothing will be scheduled", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The four window switches.</summary>
    private static IReadOnlyList<AutomationSetting> WindowRows(
        IReadOnlyList<AutomationSetting> rows) =>
        rows.ToList();
}
