using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.UI.Phone.Messages;
using UnityEngine;

namespace Dealcraft;

/// <summary>
/// Reads the deal windows off the game: how many there are, how long they run,
/// what hours each one covers, and which ones the game is offering right now.
/// </summary>
/// <remarks>
/// <para>
/// No hour is written down in this mod. Everything below comes from
/// <c>Il2CppScheduleOne.Economy.DealWindowInfo</c> and
/// <c>Il2CppScheduleOne.GameTime.TimeManager</c> at runtime, so a player who
/// reads "Afternoon, 12:00 PM to 6:00 PM" is reading the game, and a patch that
/// moves a window moves what the mod shows.
/// </para>
/// <para>
/// Whether a window is on offer is not restated here either: the mod asks the
/// player's own <c>DealWindowSelector</c> through its <c>IsWindowValid</c>, so
/// a human and the automation cannot end up with different lists of what was
/// available.
/// </para>
/// </remarks>
internal sealed class DealWindowReader
{
    private readonly Action<string> _warn;

    /// <summary>
    /// The selector is a scene object and dies with the scene, so it is found
    /// again rather than kept across one. Searching the whole scene is not
    /// something to do per frame, which is why it is cached at all.
    /// </summary>
    private DealWindowSelector _selector;

    /// <summary>The last complaint made, so it is not repeated every frame.</summary>
    private string _complained = string.Empty;

    public DealWindowReader(Action<string> warn)
    {
        _warn = warn;
    }

    /// <summary>Forget the cached selector, because the scene it lived in is gone.</summary>
    public void Forget()
    {
        _selector = null;
        _complained = string.Empty;
    }

    /// <summary>
    /// The hours of every window, as the game reports them. Empty when the game
    /// could not be read — a caller showing these says so rather than inventing
    /// hours.
    /// </summary>
    public IReadOnlyList<DealWindowHours> Hours()
    {
        try
        {
            var hours = new List<DealWindowHours>(DealWindowSet.All.Count);
            foreach (DealWindow window in DealWindowSet.All)
            {
                DealWindowInfo info = DealWindowInfo.GetWindowInfo((EDealWindow)(int)window);
                hours.Add(new DealWindowHours(
                    window,
                    info.StartTime,
                    info.EndTime,
                    startLabel: Spell(info.StartTime),
                    endLabel: Spell(info.EndTime)));
            }

            return hours;
        }
        catch (Exception error)
        {
            Complain($"the deal windows could not be read ({error.Message})");
            return Array.Empty<DealWindowHours>();
        }
    }

    /// <summary>
    /// How many windows the game has and how long each runs, for the log. Both
    /// sit next to the hours in <c>DealWindowInfo</c>; neither is assumed.
    /// </summary>
    public string Shape()
    {
        try
        {
            return $"{DealWindowInfo.WINDOW_COUNT} windows of "
                + $"{DealWindowInfo.WINDOW_DURATION_MINS} minutes each";
        }
        catch (Exception error)
        {
            return $"window count unread ({error.Message})";
        }
    }

    /// <summary>
    /// The windows the game is currently offering, which is exactly the set of
    /// buttons a player would find enabled. An empty list means none of them —
    /// which is a real answer late in a window, not a failure.
    /// </summary>
    public IReadOnlyList<DealWindow> OnOffer()
    {
        DealWindowSelector selector = Selector();
        if (selector == null)
        {
            // Without the game's own answer there is nothing honest to say, and
            // guessing is the one thing that would let the automation and a
            // player disagree about what was on offer.
            Complain("no deal window selector is in this scene, so the game cannot be "
                + "asked which windows it is offering");
            return Array.Empty<DealWindow>();
        }

        try
        {
            var offered = new List<DealWindow>(DealWindowSet.All.Count);
            foreach (DealWindow window in DealWindowSet.All)
            {
                if (selector.IsWindowValid((EDealWindow)(int)window))
                {
                    offered.Add(window);
                }
            }

            return offered;
        }
        catch (Exception error)
        {
            _selector = null;
            Complain($"the deal window selector could not be asked ({error.Message})");
            return Array.Empty<DealWindow>();
        }
    }

    /// <summary>
    /// Whether a player has the window selector open right now. While they do,
    /// the automation stands well clear: they are choosing a window by hand and
    /// the mod must not answer for them.
    /// </summary>
    public bool APlayerIsChoosing()
    {
        try
        {
            DealWindowSelector selector = Selector();
            return selector != null && selector.IsOpen;
        }
        catch (Exception)
        {
            // A selector that went away mid-read is not one a player has open.
            _selector = null;
            return false;
        }
    }

    /// <summary>
    /// The window the game's clock is in right now, or null when the game could
    /// not be asked.
    /// </summary>
    /// <remarks>
    /// The same two calls the game makes of itself, and nothing in between:
    /// <c>TimeManager.CurrentTime</c> is the HHMM the clock shows, and
    /// <c>DealWindowInfo.GetWindow</c> is the game's own mapping
    /// from an HHMM to a window. No hour is written down here, so a patch that
    /// moves a window moves what the scheduler counts from. Null is a real
    /// answer and the scheduler treats it as one — it starts at the top of the
    /// day and says in the log that it could not count from now.
    /// </remarks>
    public DealWindow? CurrentWindow()
    {
        try
        {
            if (!NetworkSingleton<TimeManager>.InstanceExists)
            {
                Complain("the game's clock is not up, so the next deal window cannot be "
                    + "counted from now");
                return null;
            }

            TimeManager time = NetworkSingleton<TimeManager>.Instance;
            if (time == null)
            {
                Complain("the game's clock is not up, so the next deal window cannot be "
                    + "counted from now");
                return null;
            }

            return WindowOf(time.CurrentTime);
        }
        catch (Exception error)
        {
            Complain($"the game's clock could not be read ({error.Message})");
            return null;
        }
    }

    /// <summary>
    /// Which window a contract was scheduled into, by the game's own mapping
    /// from a clock time to a window.
    /// </summary>
    public DealWindow? WindowOf(int windowStartTime)
    {
        try
        {
            return (DealWindow)(int)DealWindowInfo.GetWindow(windowStartTime);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private DealWindowSelector Selector()
    {
        if (_selector != null)
        {
            return _selector;
        }

        try
        {
            // Inactive too: the selector spends most of its life switched off,
            // and it answers IsWindowValid either way.
            _selector = UnityEngine.Object.FindObjectOfType<DealWindowSelector>(true);
        }
        catch (Exception error)
        {
            Complain($"the deal window selector could not be found ({error.Message})");
        }

        return _selector;
    }

    /// <summary>The game's own spelling of a time, or nothing to fall back on.</summary>
    private static string Spell(int time)
    {
        try
        {
            return TimeManager.Get12HourTime(time, appendDesignator: true);
        }
        catch (Exception)
        {
            // The core spells the raw HHMM instead, which is the same reading.
            return null;
        }
    }

    /// <summary>Say once what is wrong, however many frames it stays wrong.</summary>
    private void Complain(string problem)
    {
        if (problem == _complained)
        {
            return;
        }

        _complained = problem;
        _warn(problem);
    }
}
