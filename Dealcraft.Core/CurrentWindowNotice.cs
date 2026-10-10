using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Whether the window the clock is in still has time enough to take a deal.
/// </summary>
/// <remarks>
/// <para>
/// The game offers a window when its next start is more than two hours away,
/// and once a window has started it offers it until the window ends. So taken on
/// the game's word alone, the window the clock stands in is on offer at 17:50
/// for an Afternoon that ends at 18:00 — ten minutes to reach a customer who may
/// be across the map.
/// </para>
/// <para>
/// <b>The figure is the game's, not one chosen here.</b> Two hours is the notice
/// the game demands before it will put a deal into a window that has not started
/// yet. The same notice is asked of a window that has: it is taken while at least
/// that much of it is left, and after that the walk moves on to the next one.
/// </para>
/// </remarks>
public static class CurrentWindowNotice
{
    /// <summary>
    /// Minutes of notice: the game's own, for a window that has not started.
    /// </summary>
    public const int Minutes = 120;

    /// <summary>
    /// Minutes left in <paramref name="window"/> at <paramref name="clock"/>, or
    /// null when the clock is not inside it or the hours were not reported.
    /// </summary>
    /// <param name="clock">The game's clock as HHMM.</param>
    public static int? MinutesLeft(int clock, DealWindowHours window)
    {
        if (!window.Available)
        {
            return null;
        }

        int now = InMinutes(clock);
        int start = InMinutes(window.StartTime);
        int end = InMinutes(window.EndTime);

        // A window that ends at midnight is reported as ending at 0 or at 2400;
        // either way it ends at the end of the day.
        if (end == 0 || end == 1440)
        {
            end = 1440;
        }

        if (now < start || now >= end)
        {
            return null;
        }

        return end - now;
    }

    /// <summary>
    /// The windows on offer, less the one the clock is in when too little of it
    /// is left. Unchanged when nothing is known about the time left.
    /// </summary>
    public static IReadOnlyList<DealWindow> Offerable(
        IReadOnlyList<DealWindow> onOffer,
        DealWindow? now,
        int? minutesLeft)
    {
        if (onOffer is null || now is null || minutesLeft is null || minutesLeft >= Minutes)
        {
            return onOffer ?? new List<DealWindow>();
        }

        var kept = new List<DealWindow>(onOffer.Count);

        for (int i = 0; i < onOffer.Count; i++)
        {
            if (onOffer[i] != now.Value)
            {
                kept.Add(onOffer[i]);
            }
        }

        return kept;
    }

    private static int InMinutes(int hhmm) => (hhmm / 100 * 60) + (hhmm % 100);
}
