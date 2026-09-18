using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// What a row will accept typed into it: which entry it writes, the ends it
/// will take, and how the number the player types relates to the number the
/// file holds.
/// </summary>
/// <remarks>
/// <para>
/// A plain value, like <see cref="FormRow"/> itself. The renderer hands back
/// whatever was typed and decides nothing; <see cref="AutomationForm.Type"/>
/// reads it against this and answers with the write, or with nothing at all.
/// </para>
/// <para>
/// <b>Out of range reverts and never clamps.</b> Nothing here produces the
/// nearest end: an entry outside the range, or one that is not a number, writes
/// nothing, and the field goes back to the last valid value. Clamping silently
/// turns a typo into a setting — a player who meant 80 and typed 30 would get
/// 50 and never learn that 30 was refused.
/// </para>
/// </remarks>
public readonly struct TypedEntry
{
    public TypedEntry(string key, float lowest, float highest, float per = 1f)
    {
        Key = key;
        Lowest = lowest;
        Highest = highest;
        Per = per;
    }

    /// <summary>The MelonPreferences entry a valid entry writes.</summary>
    public string Key { get; }

    /// <summary>The lowest the player may type. Inclusive.</summary>
    public float Lowest { get; }

    /// <summary>The highest the player may type. Inclusive.</summary>
    public float Highest { get; }

    /// <summary>
    /// How many of the units the player types make one of the units the file
    /// holds. One where they are the same unit; a hundred where the player
    /// types whole percent and the file holds a fraction.
    /// </summary>
    public float Per { get; }

    /// <summary>Whether this row takes typing at all.</summary>
    public bool Takes => !string.IsNullOrEmpty(Key) && Highest > Lowest;

    /// <summary>
    /// What typing <paramref name="text"/> into this row writes, or nothing —
    /// and nothing means the field goes back to what the file still says.
    /// </summary>
    /// <remarks>
    /// Invariant, and the same parse in the app as in the file. A player whose
    /// machine writes decimals with a comma would otherwise be able to type a
    /// figure the preferences file cannot read back. The unit the row is drawn
    /// in comes off too, because the row draws one: a field showing <c>90%</c>
    /// that refused <c>90%</c> typed back into it would be refusing its own
    /// value.
    /// </remarks>
    public SettingChange? Write(string? text)
    {
        if (!Takes || text is null)
        {
            return null;
        }

        if (!float.TryParse(
                text.Trim().TrimEnd('%'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float typed))
        {
            return null;
        }

        // NaN is neither below the bottom nor above the top, so it is named.
        if (float.IsNaN(typed) || typed < Lowest || typed > Highest)
        {
            return null;
        }

        return new SettingChange(Key, Figures.Setting(typed / Per));
    }
}
