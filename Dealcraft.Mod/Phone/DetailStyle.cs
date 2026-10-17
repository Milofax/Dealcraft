using Il2CppScheduleOne.DevUtilities;
using UnityEngine;
using UnityEngine.UI;

namespace Dealcraft.Phone;

/// <summary>
/// The type and the two colours Dealcraft's page is set in, all of them taken
/// from the panel being extended. Nothing here is constructed or styled by hand:
/// a heading is a copy of that panel's heading, a label is a copy of its body
/// label, and the two colours come from its <c>ColorFont</c>.
/// </summary>
/// <remarks>
/// It lived inside <c>DetailView.cs</c> until the two tabs were deleted, which
/// was a file about the detail panel — and it is the settings page that reads
/// this type, for its headings, its body type and its two tones. A style the
/// whole app is drawn out of does not belong inside one of the things drawn
/// with it.
/// </remarks>
internal sealed class DetailStyle
{
    /// <summary>The type a section header is set in.</summary>
    public Text Heading;

    /// <summary>The type a label and a value are set in.</summary>
    public Text Body;

    /// <summary>What a figure is written in.</summary>
    public Color Accent = Color.white;

    /// <summary>What a label beside a figure is written in.</summary>
    public Color Muted = Color.grey;

    /// <summary>
    /// The heading, the body label and the colours one panel writes them in,
    /// read off that panel.
    /// </summary>
    public static DetailStyle Of(Text heading, Text body, ColorFont palette) => new()
    {
        Heading = heading,
        Body = body,
        Accent = PanelPalette.Accent(palette, body),
        Muted = PanelPalette.Muted(palette, body),
    };
}
