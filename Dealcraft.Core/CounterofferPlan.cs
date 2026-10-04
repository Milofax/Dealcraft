using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// Turns a point on the price curve into the offer to make, and says which
/// quantities the curve should be drawn over in the first place.
/// </summary>
/// <remarks>
/// <para>
/// One path, and it is now the only one. The counteroffer overlay's Apply
/// button came through here too, so the offer a player took by hand and the
/// offer the host sent on its own were the same pair of numbers by construction
/// rather than by two implementations agreeing. The overlay was deleted with
/// ticket 27; the reason the shared path was worth having is in the history, and
/// what is left is the host-side automation alone.
/// </para>
/// <para>
/// Pure, like everything else in this assembly: it knows the screen's limits as
/// four numbers and knows nothing about a screen.
/// </para>
/// </remarks>
public static class CounterofferPlan
{
    /// <summary>
    /// Which quantity to ask the customer about: the one they asked for.
    /// </summary>
    /// <remarks>
    /// There is one goal, the most money, and price optimisation is how it is
    /// reached — so the advisor prices the deal in front of it and never talks
    /// the customer into a different size to chase a bigger total. The switch
    /// that used to search every quantity the screen allows, and the target
    /// quantity that capped it, are both gone: a second goal the player could
    /// not see was the only thing they served.
    /// </remarks>
    /// <param name="limits">What the screen will hold.</param>
    /// <param name="quantityOnScreen">The quantity currently offered.</param>
    public static QuantityRange Search(in CounterofferLimits limits, int quantityOnScreen)
    {
        if (!limits.Viable)
        {
            return QuantityRange.None;
        }

        // Through the screen's own clamp first: a field reading 9999 on a screen
        // that holds twenty is a deal of twenty, and that is the deal to price.
        int held = ScreenLimits.Quantity(quantityOnScreen, limits.MinQuantity, limits.MaxQuantity);
        return new QuantityRange(held, held);
    }

    /// <summary>
    /// The offer to make, with the screen's limits already applied to it.
    /// </summary>
    /// <remarks>
    /// Both numbers come back as the screen's controls would hold them — the
    /// quantity clamped, the total clamped and rounded to a whole unit. So
    /// there is nothing left for the screen to change when the button applies
    /// it, and nothing the server would see differently when the automation
    /// sends it.
    /// </remarks>
    public static PlannedOffer Offer(CurveChoice recommendation, in CounterofferLimits limits)
    {
        if (!limits.Viable)
        {
            return PlannedOffer.None(limits.Problem);
        }

        if (!recommendation.HasOffer)
        {
            return PlannedOffer.None(recommendation.Reason);
        }

        int quantity = ScreenLimits.Quantity(
            recommendation.Quantity, limits.MinQuantity, limits.MaxQuantity);
        float price = ScreenLimits.Price(
            recommendation.TotalPrice, limits.MinPrice, limits.MaxPrice);

        return PlannedOffer.Offer(quantity, price, Because(recommendation, quantity, price));
    }

    /// <summary>
    /// The curve's own reason, and then what the screen did to it. The search
    /// works inside these limits already, so in the ordinary case it did
    /// nothing and there is nothing extra to say.
    /// </summary>
    private static string Because(CurveChoice recommendation, int quantity, float price)
    {
        if (quantity == recommendation.Quantity && price == recommendation.TotalPrice)
        {
            return recommendation.Reason;
        }

        return $"{recommendation.Reason}; the screen takes it as {quantity} at {Money(price)} "
            + $"rather than {recommendation.Quantity} at {Money(recommendation.TotalPrice)}";
    }

    private static string Money(float amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
