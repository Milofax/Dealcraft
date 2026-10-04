using System;
using System.Globalization;
using Dealcraft.Core;
using MelonLoader;

namespace Dealcraft;

/// <summary>
/// Puts a value the app asked for into the MelonPreferences entry that owns it.
/// </summary>
/// <remarks>
/// <para>
/// The owner is found by <see cref="MelonPreferences_Entry.Identifier"/> rather
/// than by a second list of entry names written down somewhere. That matters:
/// the spec's rule is that a setting is in the file and in the app and in
/// neither place twice, and a lookup table of "which key is which field" would
/// be exactly the third copy that rule exists to forbid. Whatever
/// <c>Declare</c> created is what can be written, by construction.
/// </para>
/// <para>
/// Nothing here flushes. Writing and flushing are separated so that one press
/// costs one write to disk however many entries were offered the change —
/// <see cref="ModSettings.Change"/> is the one place that saves the category.
/// </para>
/// </remarks>
internal static class Preference
{
    /// <summary>
    /// Give the change to whichever of these entries is named by it. Returns
    /// whether one of them took it; a key none of them owns is not an error
    /// here, it just belongs to somebody else.
    /// </summary>
    public static bool Set(SettingChange change, params MelonPreferences_Entry[] entries)
    {
        foreach (MelonPreferences_Entry entry in entries)
        {
            if (entry is null || !string.Equals(entry.Identifier, change.Key, StringComparison.Ordinal))
            {
                continue;
            }

            return Assign(entry, change.Value);
        }

        return false;
    }

    /// <summary>
    /// Write the text into the entry, in the entry's own type. A value that
    /// will not parse is refused rather than written as something else: the app
    /// only ever offers values it spelled itself, so this failing means the two
    /// halves have gone out of step, and a refused write leaves the file saying
    /// what it said.
    /// </summary>
    private static bool Assign(MelonPreferences_Entry entry, string value)
    {
        switch (entry)
        {
            case MelonPreferences_Entry<bool> flag when TryFlag(value, out bool state):
                flag.Value = state;
                return true;

            case MelonPreferences_Entry<int> whole
                when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number):
                whole.Value = number;
                return true;

            case MelonPreferences_Entry<float> fraction
                when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float figure):
                fraction.Value = figure;
                return true;

            case MelonPreferences_Entry<string> text:
                text.Value = value;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// A switch as the app spells it. On and Off are what the row shows, so
    /// they are what it offers back; true and false are accepted as well
    /// because that is how the file itself holds them.
    /// </summary>
    private static bool TryFlag(string value, out bool state)
    {
        if (string.Equals(value, "On", StringComparison.OrdinalIgnoreCase))
        {
            state = true;
            return true;
        }

        if (string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase))
        {
            state = false;
            return true;
        }

        return bool.TryParse(value, out state);
    }
}
