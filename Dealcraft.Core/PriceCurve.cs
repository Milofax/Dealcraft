using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// What one quantity is worth: the highest total the customer accepted for it,
/// and what that comes to per unit.
/// </summary>
public readonly struct PricePoint
{
    public PricePoint(int quantity, bool accepted, float totalPrice, int probes)
    {
        Quantity = quantity;
        Accepted = accepted;
        TotalPrice = totalPrice;
        Probes = probes;
    }

    public int Quantity { get; }

    /// <summary>
    /// False when no price in the search range reached the confidence the
    /// curve was drawn at. Not a refusal: the probe is
    /// <c>GetOfferSuccessChance</c> against a threshold, and the game's own
    /// accept-or-refuse call rolls a die (<c>docs/native-truth.md</c>).
    /// </summary>
    public bool Accepted { get; }

    /// <summary>The highest accepted total, or zero when nothing was accepted.</summary>
    public float TotalPrice { get; }

    /// <summary>The figure the player thinks in, and the one the floor is measured against.</summary>
    public float PricePerUnit => DealPrice.PerUnit(TotalPrice, Quantity);

    /// <summary>How many prices were examined to reach this answer.</summary>
    public int Probes { get; }
}

/// <summary>
/// The customer's answer across every feasible quantity. Built by probing, so
/// it holds whatever shape the customer actually has.
/// </summary>
public sealed class PriceCurve
{
    public PriceCurve(IReadOnlyList<PricePoint> points, int probes, bool budgetExhausted = false)
    {
        Points = points;
        Probes = probes;
        BudgetExhausted = budgetExhausted;
    }

    public IReadOnlyList<PricePoint> Points { get; }

    /// <summary>Total probes spent building the curve. Bounded; worth logging.</summary>
    public int Probes { get; }

    /// <summary>
    /// True when <see cref="OfferBounds.MaxProbes"/> ran out before the search
    /// finished. The points here were all genuinely probed — nothing is
    /// inferred — but quantities may be missing and a price may be lower than
    /// the customer would in fact have paid. Never an error; the caller reports
    /// it so the player knows the answer is the best of a partial look.
    /// </summary>
    public bool BudgetExhausted { get; }
}
