using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Which of the four deal windows the automation is allowed to use at all.
/// </summary>
/// <remarks>
/// This is the headline difference from the original, which applies a single
/// integer to every deal forever. Here each window is on or off on its own, and
/// the scheduler only ever picks from what is on. All four off is a real answer
/// and means "do not auto-schedule" — see <see cref="AllowsNothing"/>, which the
/// scheduler turns into a line in the log rather than silence.
/// </remarks>
public sealed class DealWindowSet
{
    /// <summary>
    /// The four windows in the order the game day runs, which is also the order
    /// the player's own selector lays its buttons out in. Everything that walks
    /// the windows walks them through here, so there is one order, not several.
    /// </summary>
    public static IReadOnlyList<DealWindow> All { get; } = new[]
    {
        DealWindow.Morning,
        DealWindow.Afternoon,
        DealWindow.Night,
        DealWindow.LateNight,
    };

    private readonly HashSet<DealWindow> allowed = new();

    /// <summary>Whether the scheduler may use this window.</summary>
    public bool Allows(DealWindow window) => allowed.Contains(window);

    /// <summary>Whether no window at all is allowed, so nothing can be scheduled.</summary>
    public bool AllowsNothing => allowed.Count == 0;

    /// <summary>
    /// Every allowed window, in the day's order. Built on demand: this is read
    /// once per scheduling decision, not per frame.
    /// </summary>
    public IReadOnlyList<DealWindow> Allowed
    {
        get
        {
            var result = new List<DealWindow>(allowed.Count);
            foreach (DealWindow window in All)
            {
                if (allowed.Contains(window))
                {
                    result.Add(window);
                }
            }

            return result;
        }
    }

    /// <summary>Switch one window on or off, leaving the other three alone.</summary>
    public void Allow(DealWindow window, bool allow)
    {
        if (allow)
        {
            allowed.Add(window);
        }
        else
        {
            allowed.Remove(window);
        }
    }

    /// <summary>How the set reads in a log line or the app.</summary>
    public override string ToString()
    {
        IReadOnlyList<DealWindow> windows = Allowed;
        if (windows.Count == 0)
        {
            return "none";
        }

        var names = new List<string>(windows.Count);
        foreach (DealWindow window in windows)
        {
            names.Add(DealWindowName.Of(window));
        }

        return string.Join(", ", names);
    }
}
