using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class DealWindowHoursTests
{
    /// <summary>
    /// The point of the whole type: a player reading "Afternoon" can see which
    /// hours that is. The hours arrive from the game and are never invented
    /// here, so a patch that moves a window moves this line with it.
    /// </summary>
    [Fact]
    public void A_window_reads_as_its_name_and_the_hours_the_game_reported()
    {
        var hours = new DealWindowHours(DealWindow.Afternoon, 1200, 1800);

        Assert.Equal("Afternoon", hours.Name);
        Assert.Equal("12:00 to 18:00", hours.Range);
    }

    [Fact]
    public void Midnight_at_both_ends_reads_as_the_clock_shows_it()
    {
        var hours = new DealWindowHours(DealWindow.LateNight, 0, 600);

        Assert.Equal("Late Night", hours.Name);
        Assert.Equal("00:00 to 06:00", hours.Range);
    }

    /// <summary>
    /// The game spells the end of Night as 2400, one past the last minute of
    /// the day. Splitting that into hours and minutes naively gives "24:00",
    /// which is what the game itself shows, so that is what we show.
    /// </summary>
    [Fact]
    public void The_end_of_the_day_is_spelled_the_way_the_game_spells_it()
    {
        var hours = new DealWindowHours(DealWindow.Night, 1800, 2400);

        Assert.Equal("18:00 to 24:00", hours.Range);
    }

    /// <summary>
    /// When the adapter can get the game's own 12-hour spelling it wins, so the
    /// mod and the phone never disagree about how a time is written.
    /// </summary>
    [Fact]
    public void The_games_own_spelling_is_preferred_when_it_is_available()
    {
        var hours = new DealWindowHours(
            DealWindow.Morning, 600, 1200, startLabel: "6:00 AM", endLabel: "12:00 PM");

        Assert.Equal("6:00 AM to 12:00 PM", hours.Range);
    }

    [Fact]
    public void A_blank_label_falls_back_to_the_clock_rather_than_showing_nothing()
    {
        var hours = new DealWindowHours(
            DealWindow.Morning, 600, 1200, startLabel: "", endLabel: "   ");

        Assert.Equal("06:00 to 12:00", hours.Range);
    }

    /// <summary>
    /// A window the game could not be asked about. Zero to zero would read as a
    /// real deal at midnight, and Late Night genuinely does start at zero, so
    /// the two readings must stay apart.
    /// </summary>
    [Fact]
    public void Hours_the_game_did_not_report_say_so_rather_than_reading_as_midnight()
    {
        var unread = new DealWindowHours(DealWindow.Morning, 0, 0);

        Assert.False(unread.Available);
        Assert.Contains("not report", unread.Range, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("00:00", unread.Range);
    }

    [Fact]
    public void Late_night_really_does_start_at_zero_and_is_a_real_reading()
    {
        var lateNight = new DealWindowHours(DealWindow.LateNight, 0, 600);

        Assert.True(lateNight.Available);
    }

    [Fact]
    public void A_window_describes_itself_with_its_hours()
    {
        var hours = new DealWindowHours(DealWindow.Morning, 600, 1200);

        Assert.Equal("Morning (06:00 to 12:00)", hours.ToString());
    }

    /// <summary>
    /// Minutes are not always zero in principle, and a window is measured in
    /// them: WINDOW_DURATION_MINS sits next to the hours in the game.
    /// </summary>
    [Fact]
    public void A_time_with_minutes_keeps_them()
    {
        var hours = new DealWindowHours(DealWindow.Morning, 630, 1245);

        Assert.Equal("06:30 to 12:45", hours.Range);
    }
}
