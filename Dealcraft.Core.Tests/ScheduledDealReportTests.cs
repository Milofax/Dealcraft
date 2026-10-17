using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class ScheduledDealReportTests
{
    private static ContractTimings NightTimings() => new(
        softStartTime: 1800, hardStartTime: 1810, endTime: 2400);

    /// <summary>
    /// User story 7: the host wants the real clock time a scheduled deal starts
    /// and ends, so they can plan a route rather than guess a window. The window
    /// name alone is not that.
    /// </summary>
    [Fact]
    public void The_report_gives_the_clock_times_and_not_just_the_window_name()
    {
        string[] lines = ScheduledDealReport.Describe(
            "Mrs. Ming",
            new DealWindowHours(DealWindow.Night, 1800, 2400),
            NightTimings()).ToArray();

        Assert.Contains(lines, line => line.Contains("Mrs. Ming"));
        Assert.Contains(lines, line => line.Contains("Night"));
        Assert.Contains(lines, line => line.Contains("18:00") && line.Contains("24:00"));
    }

    /// <summary>
    /// The times the game reports for the contract, not the window's nominal
    /// hours: those are two different readings and the contract's is the one
    /// the player has to turn up for.
    /// </summary>
    [Fact]
    public void The_contracts_own_timings_are_reported_alongside_the_window()
    {
        string[] lines = ScheduledDealReport.Describe(
            "Mrs. Ming",
            new DealWindowHours(DealWindow.Night, 1800, 2400),
            new ContractTimings(softStartTime: 1800, hardStartTime: 1810, endTime: 2400)).ToArray();

        Assert.Contains(lines, line => line.Contains("18:10"));
    }

    /// <summary>
    /// The game hands back three zeroes when it has no window to read, and
    /// zeroes spelled as "00:00 to 00:00" would read as a real midnight deal.
    /// </summary>
    [Fact]
    public void Timings_the_game_could_not_supply_are_reported_as_missing()
    {
        string[] lines = ScheduledDealReport.Describe(
            "Mrs. Ming",
            new DealWindowHours(DealWindow.Night, 1800, 2400),
            ContractTimings.Unavailable).ToArray();

        Assert.Contains(lines, line =>
            line.Contains("did not report", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, line => line.Contains("00:00"));
    }

    [Fact]
    public void Timings_that_are_all_zero_count_as_missing()
    {
        var timings = new ContractTimings(0, 0, 0);

        Assert.False(timings.Available);
    }

    /// <summary>
    /// Three zeroes is the game saying it has no window to read. Any one figure
    /// that is not zero is a real reading, whichever of the three it is —
    /// a midnight start, a midnight end and a hard start ten minutes into the
    /// day are all things the game reports. Treating a partly-zero reading as
    /// missing would drop a genuine late-night deal off the report.
    /// </summary>
    [Theory]
    [InlineData(0, 10, 600)]
    [InlineData(1800, 0, 2400)]
    [InlineData(1800, 1810, 0)]
    [InlineData(0, 0, 600)]
    [InlineData(0, 10, 0)]
    [InlineData(1800, 0, 0)]
    public void A_reading_with_any_figure_at_all_is_a_reading(int soft, int hard, int end)
    {
        Assert.True(new ContractTimings(soft, hard, end).Available);
    }

    [Fact]
    public void Every_line_is_worth_reading_and_none_is_blank()
    {
        string[] lines = ScheduledDealReport.Describe(
            "Mrs. Ming",
            new DealWindowHours(DealWindow.Night, 1800, 2400),
            NightTimings()).ToArray();

        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.False(string.IsNullOrWhiteSpace(line)));
    }
}
