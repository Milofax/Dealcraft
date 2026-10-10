using System.Collections.Generic;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The window the clock is in, taken while there is time to deliver into it.
/// </summary>
/// <remarks>
/// Taking the current window is what stops evening deals going into a Late
/// Night the owner sleeps through — ten contracts expired at once on day 44.
/// The game offers a started window to its last minute, so without a notice an
/// offer at 17:50 would go into an Afternoon with ten minutes left. The notice is
/// the game's own two hours.
/// </remarks>
public class CurrentWindowNoticeTests
{
    private static readonly DealWindowHours Afternoon = new(DealWindow.Afternoon, 1200, 1800);
    private static readonly DealWindowHours Night = new(DealWindow.Night, 1800, 0);
    private static readonly DealWindowHours NightAt2400 = new(DealWindow.Night, 1800, 2400);
    private static readonly DealWindowHours LateNight = new(DealWindow.LateNight, 0, 600);

    private static readonly DealWindow[] All =
    {
        DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight,
    };

    [Theory]
    [InlineData(1819, 341)]
    [InlineData(2200, 120)]
    [InlineData(2359, 1)]
    public void Minutes_left_in_Night_count_to_midnight(int clock, int left)
    {
        Assert.Equal(left, CurrentWindowNotice.MinutesLeft(clock, Night));
        Assert.Equal(left, CurrentWindowNotice.MinutesLeft(clock, NightAt2400));
    }

    [Fact]
    public void Outside_the_window_there_is_no_answer()
    {
        Assert.Null(CurrentWindowNotice.MinutesLeft(1750, Night));
        Assert.Null(CurrentWindowNotice.MinutesLeft(1800, Afternoon));
        Assert.Null(CurrentWindowNotice.MinutesLeft(700, LateNight));
    }

    [Fact]
    public void Late_Night_counts_from_midnight_to_six()
    {
        Assert.Equal(360, CurrentWindowNotice.MinutesLeft(0, LateNight));
        Assert.Equal(90, CurrentWindowNotice.MinutesLeft(430, LateNight));
    }

    [Fact]
    public void Hours_the_game_did_not_report_answer_nothing()
    {
        Assert.Null(CurrentWindowNotice.MinutesLeft(1900, new DealWindowHours(DealWindow.Night, 0, 0)));
    }

    /// <summary>
    /// The owner's evening: 18:19, Night with nearly six hours left, stays on offer.
    /// </summary>
    [Fact]
    public void A_window_with_hours_left_stays_on_offer()
    {
        IReadOnlyList<DealWindow> kept = CurrentWindowNotice.Offerable(All, DealWindow.Night, 341);

        Assert.Equal(All, kept);
    }

    /// <summary>
    /// 17:50, Afternoon with ten minutes left, is taken off the list, so the walk
    /// moves on to the next window rather than handing over a deal nobody can
    /// reach in time.
    /// </summary>
    [Fact]
    public void A_window_nearly_over_comes_off_the_list()
    {
        IReadOnlyList<DealWindow> kept = CurrentWindowNotice.Offerable(All, DealWindow.Afternoon, 10);

        Assert.DoesNotContain(DealWindow.Afternoon, kept);
        Assert.Equal(3, kept.Count);
    }

    [Fact]
    public void Exactly_the_notice_left_is_still_enough()
    {
        Assert.Contains(DealWindow.Night, CurrentWindowNotice.Offerable(All, DealWindow.Night, 120));
        Assert.DoesNotContain(DealWindow.Night, CurrentWindowNotice.Offerable(All, DealWindow.Night, 119));
    }

    [Fact]
    public void Without_a_clock_nothing_is_taken_away()
    {
        Assert.Equal(All, CurrentWindowNotice.Offerable(All, null, 10));
        Assert.Equal(All, CurrentWindowNotice.Offerable(All, DealWindow.Night, null));
    }

    /// <summary>
    /// End to end through the walk: at 17:50 with every window ticked, the
    /// Afternoon's last ten minutes are not taken.
    /// </summary>
    [Fact]
    public void At_ten_to_six_the_last_ten_minutes_of_Afternoon_are_not_taken()
    {
        var allowed = new DealWindowSet();
        foreach (DealWindow window in All)
        {
            allowed.Allow(window, true);
        }

        IReadOnlyList<DealWindow> onOffer = CurrentWindowNotice.Offerable(
            new[] { DealWindow.Morning, DealWindow.Afternoon, DealWindow.LateNight },
            DealWindow.Afternoon,
            CurrentWindowNotice.MinutesLeft(1750, Afternoon));

        ScheduleDecision decision = new DealWindowRotation().Choose(allowed, onOffer, DealWindow.Afternoon);

        // Night is not on offer at 17:50 — it starts in ten minutes, inside the
        // game's own two hours — so the walk goes past it to Late Night.
        Assert.Equal(ScheduleOutcome.Schedule, decision.Outcome);
        Assert.Equal(DealWindow.LateNight, decision.Window);
    }
}
