using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// One window and the clock times the game reports for it.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here decides what a window's hours are. The adapter reads them off
/// <c>Il2CppScheduleOne.Economy.DealWindowInfo</c> at runtime and hands them
/// over; this type only knows how to say them. That is the whole point: a
/// player who reads "Afternoon" must be able to see which hours that is, and
/// must still be right after a patch moves them.
/// </para>
/// <para>
/// <paramref name="startLabel"/> and <paramref name="endLabel"/> are the game's
/// own spelling, from <c>TimeManager.Get12HourTime</c>. They win when present
/// so that the mod writes a time the way the phone writes it. Without them the
/// raw HHMM integer is split into hours and minutes, which is the same reading,
/// just on a 24-hour clock.
/// </para>
/// </remarks>
public readonly struct DealWindowHours
{
    private readonly string? startLabel;
    private readonly string? endLabel;

    public DealWindowHours(
        DealWindow window,
        int startTime,
        int endTime,
        string? startLabel = null,
        string? endLabel = null)
    {
        Window = window;
        StartTime = startTime;
        EndTime = endTime;
        this.startLabel = startLabel;
        this.endLabel = endLabel;
    }

    public DealWindow Window { get; }

    /// <summary>The game's HHMM integer for the first minute of the window.</summary>
    public int StartTime { get; }

    /// <summary>The game's HHMM integer for the end of the window.</summary>
    public int EndTime { get; }

    /// <summary>The window's name as a player reads it.</summary>
    public string Name => DealWindowName.Of(Window);

    /// <summary>
    /// Whether the game supplied hours at all. Zero to zero is what a window
    /// that could not be read looks like; Late Night genuinely starts at zero
    /// but does not end there, so the two stay apart.
    /// </summary>
    public bool Available => StartTime != 0 || EndTime != 0;

    /// <summary>When the window starts, as the game spells it.</summary>
    public string Start => GameClock.Spell(startLabel, StartTime);

    /// <summary>When the window ends, as the game spells it.</summary>
    public string End => GameClock.Spell(endLabel, EndTime);

    /// <summary>The hours alone, for a line that already names the window.</summary>
    public string Range => Available ? $"{Start} to {End}" : "hours the game did not report";

    public override string ToString() => $"{Name} ({Range})";

    /// <summary>
    /// Pick one window's hours out of a reading of all of them, or null when
    /// that reading does not cover it — which is what an empty reading from a
    /// game that was not there looks like.
    /// </summary>
    public static DealWindowHours? Find(IReadOnlyList<DealWindowHours> hours, DealWindow window)
    {
        for (int i = 0; i < hours.Count; i++)
        {
            if (hours[i].Window == window)
            {
                return hours[i];
            }
        }

        return null;
    }
}
