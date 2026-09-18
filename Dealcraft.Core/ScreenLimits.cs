using System;

namespace Dealcraft.Core;

/// <summary>
/// What the counteroffer screen's own two controls do to a number, mirrored so
/// that a caller can know the answer without touching the screen.
/// </summary>
/// <remarks>
/// <para>
/// The advisor has to put an offer on the screen and the automation has to send
/// the same offer without one, so the two would drift apart the moment either
/// guessed at what the controls do with the number handed to them. They do two
/// things — a clamp and a rounding — and both were read out of the shipped
/// machine code rather than assumed from the member names:
/// </para>
/// <list type="bullet">
/// <item>
/// <c>CounterofferInterface.ChangeQuantity</c> (RVA 0x9DD790 and 0x9DD810)
/// parses, truncates towards zero with <c>cvttss2si</c>, and clamps to a
/// hardcoded floor of one and to the screen's own <c>MaxQuantity</c>. Note the
/// floor: the literal 1 is in the instruction stream, so a screen claiming to
/// allow zero still cannot offer it.
/// </item>
/// <item>
/// <c>AmountSelector.SetAmount</c> (RVA 0x9658B0) clamps to the selector's
/// <c>MinValue</c> and <c>MaxValue</c> and then rounds to a whole number —
/// <c>Mathf.RoundToInt</c>, which is round-half-to-even — and keeps the integer.
/// The package total the screen holds is therefore always whole, whatever was
/// asked for.
/// </item>
/// </list>
/// <para>
/// Both are idempotent, which is where "applying twice does not compound" comes
/// from: feeding a control the number it already holds asks it for no change.
/// </para>
/// </remarks>
public static class ScreenLimits
{
    /// <summary>
    /// The smallest quantity the screen will hold, whatever it is asked for.
    /// The game's own floor, and not configurable there or here.
    /// </summary>
    public const int SmallestQuantity = 1;

    /// <summary>
    /// The quantity the screen would end up holding if it were asked for
    /// <paramref name="wanted"/>.
    /// </summary>
    /// <param name="minQuantity">
    /// The screen's smallest offerable quantity. Raises the floor; it can never
    /// lower it below <see cref="SmallestQuantity"/>.
    /// </param>
    /// <param name="maxQuantity">The screen's largest offerable quantity.</param>
    public static int Quantity(float wanted, int minQuantity, int maxQuantity)
    {
        int floor = Math.Max(SmallestQuantity, minQuantity);
        int asked = Truncate(wanted);

        // The game's order, which matters when the ceiling is below the floor:
        // the floor wins. A caller in that position has no offer to make at all,
        // which is what CounterofferLimits refuses before it gets here.
        if (asked < floor)
        {
            return floor;
        }

        return asked > maxQuantity ? maxQuantity : asked;
    }

    /// <summary>
    /// The package total the price selector would end up holding if it were
    /// asked for <paramref name="wanted"/>: clamped into the selector's range
    /// and then rounded to a whole unit.
    /// </summary>
    public static float Price(float wanted, float minPrice, float maxPrice)
    {
        // Not a number is not a price. The game's comparisons are unordered
        // here and its rounding of a NaN is anyone's guess, so it is settled at
        // the bottom of the range instead of carried any further.
        if (float.IsNaN(wanted))
        {
            return minPrice;
        }

        float inRange = minPrice > wanted
            ? minPrice
            : wanted > maxPrice ? maxPrice : wanted;

        return (float)Math.Round((double)inRange, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Towards zero, like the cast the screen makes of its parsed input, and
    /// without the undefined result an out-of-range cast has. Every caller
    /// clamps immediately afterwards, so saturating here and clamping there
    /// reach the same number the game does.
    /// </summary>
    private static int Truncate(float wanted)
    {
        if (float.IsNaN(wanted))
        {
            return 0;
        }

        if (wanted <= int.MinValue)
        {
            return int.MinValue;
        }

        return wanted >= int.MaxValue ? int.MaxValue : (int)wanted;
    }
}
