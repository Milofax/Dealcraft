using System;

namespace Dealcraft.Core;

/// <summary>
/// What activating an Automation row does to it: the value the row would take
/// next, spelled exactly as the preferences file spells it.
/// </summary>
/// <remarks>
/// <para>
/// The app's only affordance is a row, so a change is usually "advance to the
/// next value" — the second click on a row that is already selected. A switch
/// has one other state and flips to it. A number walks a ladder of the values a
/// dealer would actually choose and wraps round at the top, so one button is
/// enough for it too.
/// </para>
/// <para>
/// <b>A range is the exception, and it has two directions.</b>
/// <see cref="Range"/> is for a control drawn with its own <c>−</c> and <c>+</c>
/// where the ends mean something — the chance floor, whose bottom is where the
/// confidence ladder starts and whose top is certainty. Wrapping there would
/// turn one press at 100% into 50%, which is the opposite of what the player
/// asked for, so both ends are blunt: <see cref="Next"/> is empty at the top and
/// <see cref="Previous"/> is empty at the bottom.
/// </para>
/// <para>
/// <see cref="Next"/> is written back verbatim, which is why it is spelled here
/// with the same formatter the row's displayed value uses: the app is a reading
/// of the file, and a value that went in spelled differently from the way it
/// comes out would be a second store by the back door.
/// </para>
/// </remarks>
public sealed class AutomationChoice
{
    /// <summary>
    /// Rungs closer together than this would be indistinguishable to a player
    /// anyway, and the tolerance is what keeps a value that came back from the
    /// file a hair off its own rung from stepping onto the same rung again.
    /// </summary>
    private const float SameRung = 0.0001f;

    private AutomationChoice(bool canChange, string next, string previous = "")
    {
        CanChange = canChange;
        Next = next;
        Previous = previous;
    }

    /// <summary>
    /// A setting the app cannot change: one that needs free text or a key press,
    /// which the phone has nowhere to take. Its row says so and the file is where
    /// it is set.
    /// </summary>
    public static AutomationChoice FileOnly { get; } = new(false, string.Empty);

    /// <summary>Whether the app can change this setting at all.</summary>
    public bool CanChange { get; }

    /// <summary>
    /// The value activating the row writes back. Empty when
    /// <see cref="CanChange"/> is false, and empty at the top of a
    /// <see cref="Range"/>, where the control's <c>+</c> is meant to do nothing.
    /// </summary>
    public string Next { get; }

    /// <summary>
    /// The value the other direction writes back, for a control that has two.
    /// Empty for everything else, and empty at the bottom of a
    /// <see cref="Range"/>.
    /// </summary>
    public string Previous { get; }

    /// <summary>A switch, which has exactly one other state.</summary>
    public static AutomationChoice Switch(bool on) =>
        new(true, AutomationSetting.OnOff(!on));

    /// <summary>
    /// A number, stepping to the first rung above where it stands and wrapping
    /// to the lowest when there is none. A value the player typed into the file
    /// by hand is not going to be a rung, and this lands it on the next one up
    /// rather than discarding it.
    /// </summary>
    public static AutomationChoice Ladder(float current, params float[] rungs)
    {
        if (rungs.Length == 0)
        {
            return FileOnly;
        }

        float lowest = rungs[0];
        float? above = null;

        foreach (float rung in rungs)
        {
            if (rung < lowest)
            {
                lowest = rung;
            }

            if (rung > current + SameRung && (above is not float best || rung < best))
            {
                above = rung;
            }
        }

        return new AutomationChoice(true, Figures.Setting(above ?? lowest));
    }

    /// <summary>
    /// A number between two ends, stepping by <paramref name="step"/>, blunt at
    /// both ends rather than wrapping.
    /// </summary>
    /// <param name="current">
    /// Where the setting stands. It need not be on the ladder: a value edited
    /// into the file by hand steps onto the next rung in whichever direction is
    /// pressed, and one outside the range altogether steps back into it — which
    /// is the only way a player could otherwise be stuck outside their own
    /// control.
    /// </param>
    public static AutomationChoice Range(float current, float lowest, float highest, float step)
    {
        if (step <= 0f || highest <= lowest)
        {
            return FileOnly;
        }

        string next = Rung(current, lowest, highest, step, upward: true);
        string previous = Rung(current, lowest, highest, step, upward: false);

        return new AutomationChoice(next.Length > 0 || previous.Length > 0, next, previous);
    }

    /// <summary>
    /// The rung one press away, or nothing where that press would leave the
    /// range. Counted in whole steps from the bottom, so the arithmetic is done
    /// once on an integer rather than accumulated a step at a time.
    /// </summary>
    private static string Rung(float current, float lowest, float highest, float step, bool upward)
    {
        double steps = (current - lowest) / (double)step;
        double top = Math.Round((highest - lowest) / (double)step);

        double wanted = upward
            ? Math.Floor(steps + SameRung) + 1
            : Math.Ceiling(steps - SameRung) - 1;

        // Outside the range in the direction being pressed is blunt; outside it
        // in the other direction is a step back onto the nearest end.
        if (wanted < 0)
        {
            if (!upward)
            {
                return string.Empty;
            }

            wanted = 0;
        }
        else if (wanted > top)
        {
            if (upward)
            {
                return string.Empty;
            }

            wanted = top;
        }

        return Figures.Setting((float)(lowest + (wanted * step)));
    }
}
