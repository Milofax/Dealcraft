using System;
using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// How a figure is spelled. One place, so every percentage the mod writes —
/// on the settings page, in the log, in the debug record — reads the same, and
/// so none of them depends on the machine's locale.
///
/// It used to name the customer panel, the product panel and the advisor
/// overlay. All three are deleted.
/// </summary>
internal static class Figures
{
    /// <summary>
    /// A fraction as whole percent, the way the game labels its bars. Held
    /// inside 0..1 first: a chance the game reported slightly outside its own
    /// range should read as certainty rather than as 101%, and the same figure
    /// drawn as a bar must not run past its track.
    /// </summary>
    public static string Percent(float fraction) =>
        Math.Round(Fraction(fraction) * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>Two decimal places, for a figure that is not money.</summary>
    public static string Fixed(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// A bare number, decimals only where there are any. For figures the game
    /// gives no unit for, such as a value proposition.
    /// </summary>
    public static string Plain(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// A setting's figure, as the preferences file holds it. Four decimals is
    /// enough for every knob the mod has and trims to none where there are none,
    /// so a row read in the app and a line read in the file are the same text —
    /// which is what lets a value the app writes back go in spelled the way it
    /// came out.
    /// </summary>
    public static string Setting(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>A fraction held inside 0..1.</summary>
    public static float Fraction(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

    /// <summary>
    /// The fallback way to spell an amount, used only when the caller supplies
    /// no formatter. In the game the adapter passes the game's own, so money
    /// reads in the game's currency.
    /// </summary>
    public static string Money(float amount) => Fixed(amount);
}
