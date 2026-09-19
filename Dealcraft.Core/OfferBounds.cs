using System;

namespace Dealcraft.Core;

/// <summary>
/// The hard limits the caller puts on the search: which quantities may be
/// offered at all, what price range may be probed, and how finely. These come
/// from the game (stock on hand, the customer's spending limit, the currency's
/// smallest step), not from the host's preferences.
/// </summary>
public readonly struct OfferBounds
{
    /// <summary>
    /// Probes a whole curve may cost when the caller names no figure. Generous
    /// enough for a normal deal — a £1 grid needs about thirteen probes a
    /// quantity, so this covers around twenty quantities — and small enough
    /// that a mistaken bound cannot turn into thousands of interop calls inside
    /// one frame. A caller who knows its own shape should say so.
    /// </summary>
    public const int DefaultMaxProbes = 256;

    /// <summary>
    /// Most quantities one curve may span. The curve holds a point per quantity
    /// and spends at least one probe on each, so a wider range describes a
    /// search no session could run; it is far above any stock a player can
    /// hold. Refusing it here keeps the failure in validation, where it can say
    /// what is wrong, instead of in a list allocation.
    /// </summary>
    public const int MaxQuantitySpan = 1_000_000;

    public OfferBounds(
        int minQuantity,
        int maxQuantity,
        float minTotalPrice,
        float maxTotalPrice,
        float priceResolution = 1f,
        int maxProbes = DefaultMaxProbes)
    {
        if (minQuantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minQuantity), minQuantity, "Quantities start at one.");
        }

        if (maxQuantity < minQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(maxQuantity), maxQuantity, "The quantity range runs upwards.");
        }

        if ((long)maxQuantity - minQuantity + 1 > MaxQuantitySpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxQuantity),
                maxQuantity,
                $"The quantity range holds more than {MaxQuantitySpan} deals, which is more curve than the " +
                "search can build.");
        }

        if (maxProbes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxProbes),
                maxProbes,
                "The search needs to be allowed at least one probe.");
        }

        if (minTotalPrice < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(minTotalPrice), minTotalPrice, "A price cannot be negative.");
        }

        if (maxTotalPrice < minTotalPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTotalPrice), maxTotalPrice, "The price range runs upwards.");
        }

        if (!(priceResolution > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(priceResolution), priceResolution, "The search needs a positive step.");
        }

        // A grid the search cannot land on exactly would answer with a price
        // the caller never asked for. Say so instead of quietly approximating.
        if (Math.Ceiling((maxTotalPrice - (double)minTotalPrice) / priceResolution) > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(priceResolution),
                priceResolution,
                "The price range needs more steps of this size than the search can address.");
        }

        MinQuantity = minQuantity;
        MaxQuantity = maxQuantity;
        MinTotalPrice = minTotalPrice;
        MaxTotalPrice = maxTotalPrice;
        PriceResolution = priceResolution;
        MaxProbes = maxProbes;
    }

    /// <summary>Smallest quantity worth offering.</summary>
    public int MinQuantity { get; }

    /// <summary>Largest quantity the player could actually deliver.</summary>
    public int MaxQuantity { get; }

    /// <summary>Lowest total price the search may name.</summary>
    public float MinTotalPrice { get; }

    /// <summary>Highest total price the search may name.</summary>
    public float MaxTotalPrice { get; }

    /// <summary>
    /// The step the search works in. Prices are only ever probed and returned
    /// on this grid, so the answer is reproducible and the probe count bounded.
    /// </summary>
    public float PriceResolution { get; }

    /// <summary>
    /// Most times the customer may be asked anything while one curve is built,
    /// across every quantity in it. Bisection bounds the probes *per* quantity
    /// at about thirteen; without this, the whole curve costs that times the
    /// quantity range, and every one of them is a synchronous call into the
    /// running game. Running out is an outcome, not a failure: the curve says
    /// so through <see cref="PriceCurve.BudgetExhausted"/> and holds the points
    /// it did reach.
    /// </summary>
    public int MaxProbes { get; }
}
