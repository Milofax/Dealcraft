using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Midnight, and what has to survive it.
///
/// The ticket's rule is "anything keyed on a day index must survive the wrap".
/// One thing in this mod touches a day: the contract key embeds the game's
/// elapsed-day counter. The scheduled windows deliberately do not — they are
/// clock times the game reports, with no day in them at all, which is why a deal
/// keeps its window when the day turns over.
///
/// There used to be a second: a customer's order days ran round a seven-day
/// week, for a row on the Customers tab. The tab is gone and so is
/// <c>OrderSchedule</c>, so the wrap it needed is not a case any more.
///
/// The key's own cases are in <see cref="ContractKeyTests"/>. These are the
/// others.
/// </summary>
public class DayRolloverTests
{
    /// <summary>
    /// The windows have no day in them, and that is the point. A deal scheduled
    /// into Late Night is scheduled into the hours the game reports for Late
    /// Night, and those hours are the same hours tomorrow — there is no
    /// parameter here that a day rolling over could move.
    /// </summary>
    [Fact]
    public void A_scheduled_deals_window_is_clock_times_with_no_day_in_them()
    {
        var lateNight = new DealWindowHours(DealWindow.LateNight, 0, 600);
        var timings = new ContractTimings(softStartTime: 0, hardStartTime: 10, endTime: 600);

        string[] reported = ScheduledDealReport.Describe("Jessi Waters", lateNight, timings).ToArray();

        Assert.Contains(reported, line => line.Contains("00:00 to 06:00"));
        Assert.Contains(reported, line => line.Contains("00:10"));
    }

    /// <summary>
    /// The window a host allows is a window, not a date. Late Night allowed
    /// today is Late Night allowed tomorrow, so nothing about the day turning
    /// over can take a window out of the allowed set.
    /// </summary>
    [Fact]
    public void An_allowed_window_stays_allowed_when_the_day_turns_over()
    {
        var allowed = new DealWindowSet();
        allowed.Allow(DealWindow.LateNight, true);

        // There is no "advance the day" to call, because there is no day here to
        // advance. Asking twice is asking the same question twice, which is what
        // makes this survivable rather than merely survived.
        Assert.True(allowed.Allows(DealWindow.LateNight));
        Assert.True(allowed.Allows(DealWindow.LateNight));
        Assert.False(allowed.Allows(DealWindow.Morning));
    }
}
