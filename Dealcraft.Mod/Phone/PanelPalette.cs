using Il2CppScheduleOne.DevUtilities;
using UnityEngine;
using UnityEngine.UI;

namespace Dealcraft.Phone;

/// <summary>
/// The colours a panel writes in, taken from its own <c>ColorFont</c>.
/// <para>
/// A <c>ColorFont</c> keeps its palette as named entries inside the asset, so
/// which names exist cannot be established from the assemblies — only from the
/// running game. A name guessed wrong would be a hardcoded colour arriving by a
/// longer road. So the entries are read at runtime and matched against a list of
/// names in preference order, and a panel whose palette has none of them falls
/// back to the colour the label being copied already carries. Dealcraft
/// therefore never writes in a colour of its own: worst case it writes in the
/// game's.
/// </para>
/// </summary>
internal static class PanelPalette
{
    /// <summary>
    /// What a figure is written in, most preferred first. Money and other
    /// values carry the game's own accent rather than plain white.
    /// </summary>
    private static readonly string[] AccentNames = { "Green", "Money", "Lime", "Positive", "White" };

    /// <summary>What a label beside a figure is written in.</summary>
    private static readonly string[] MutedNames = { "Grey", "Gray", "LightGrey", "LightGray", "Secondary", "Subtle" };

    /// <summary>
    /// The first of <paramref name="names"/> the palette knows, or the label's
    /// own colour when it knows none of them.
    /// </summary>
    public static Color Accent(ColorFont font, Text fallback) => Pick(font, AccentNames, fallback);

    public static Color Muted(ColorFont font, Text fallback) => Pick(font, MutedNames, fallback);

    private static Color Pick(ColorFont font, string[] names, Text fallback)
    {
        Color theirs = fallback != null ? fallback.color : Color.white;

        if (font == null)
        {
            return theirs;
        }

        Il2CppSystem.Collections.Generic.List<ColorFont.ColorFontItem> items = font.ColorFontItems;
        if (items is null)
        {
            return theirs;
        }

        foreach (string wanted in names)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ColorFont.ColorFontItem item = items[i];
                if (item is null || item.Name is null)
                {
                    continue;
                }

                if (string.Equals(item.Name.Trim(), wanted, System.StringComparison.OrdinalIgnoreCase))
                {
                    // The palette carries no alpha convention, and a colour that
                    // arrived fully transparent would make the label vanish.
                    Color found = item.Colour;
                    return new Color(found.r, found.g, found.b, theirs.a);
                }
            }
        }

        return theirs;
    }
}
