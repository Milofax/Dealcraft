using System;
using System.Globalization;
using Il2CppScheduleOne.Money;

namespace Dealcraft;

/// <summary>
/// Spells amounts the way the game's own UI spells them. Using the game's
/// formatter is what keeps every figure the mod shows in the game's currency
/// rather than one the mod picked; a pound sign written here would be a literal
/// that stops being true the moment the game is localised.
/// </summary>
/// <remarks>
/// Two callers wanted this and each grew its own copy, differing only in whether
/// decimals are shown. They both still want what they wanted — a panel row reads
/// better exact, a log line and an overlay read better rounded — so that is the
/// argument rather than the reason for a second helper. Colour markup is off for
/// both: it would arrive as literal tags in a plain <c>Text</c>.
/// </remarks>
internal static class GameMoney
{
    /// <summary>To the penny. What the insight panels show.</summary>
    public static string Exact(float amount) => Format(amount, showDecimals: true, fallback: "0.00");

    /// <summary>To the pound. What the overlay and the log show.</summary>
    public static string Rounded(float amount) => Format(amount, showDecimals: false, fallback: "0.##");

    /// <summary>
    /// Read a figure back out of a string the game's own UI drew.
    /// </summary>
    /// <remarks>
    /// The deal-completion popup states each bonus as a rendered label and
    /// nothing keeps the numbers behind them, so adding them up means reading
    /// the labels. The currency symbol, thousands separators and any stray
    /// markup are dropped and what is left is parsed; a label this cannot read
    /// is left out of the total rather than guessed at.
    /// </remarks>
    public static bool TryRead(string drawn, out float amount)
    {
        amount = 0f;

        if (string.IsNullOrWhiteSpace(drawn))
        {
            return false;
        }

        var figure = new System.Text.StringBuilder(drawn.Length);
        foreach (char letter in drawn)
        {
            if (char.IsDigit(letter) || letter == '.' || (letter == '-' && figure.Length == 0))
            {
                figure.Append(letter);
            }
        }

        return figure.Length > 0
            && float.TryParse(
                figure.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out amount);
    }

    /// <param name="fallback">
    /// Before a save is loaded the game's formatter may have nothing to work
    /// with. A plain figure is better than no panel.
    /// </param>
    private static string Format(float amount, bool showDecimals, string fallback)
    {
        try
        {
            return MoneyManager.FormatAmount(amount, showDecimals, includeColor: false);
        }
        catch (Exception)
        {
            return amount.ToString(fallback, CultureInfo.InvariantCulture);
        }
    }
}
