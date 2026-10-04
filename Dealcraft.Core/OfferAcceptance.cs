using System;

namespace Dealcraft.Core;

/// <summary>
/// Turns the game's success chance into the accept/refuse verdict the search
/// probes with.
///
/// The game has a method that looks like exactly this verdict —
/// <c>Customer.EvaluateCounteroffer</c> — and it must not be used for it.
/// Read off the game (), it folds a
/// <c>UnityEngine.Random.Range</c> roll into the bool it returns, so it answers
/// the same question differently from one call to the next. A bisection over a
/// predicate like that converges on noise rather than on a boundary.
///
/// <c>Customer.GetOfferSuccessChance</c> calls no Random on any path and hands
/// back the chance itself. Comparing it here against a confidence gives a
/// verdict that is deterministic and is monotone wherever the chance is.
///
/// <para>
/// <b>The comparison is unclamped, and two callers depend on that.</b> The
/// curve's probe is one; <see cref="CounterofferGate.Send"/> is the other, where
/// the player's <see cref="ChanceFloor"/> is applied to the winning offer. The
/// game's own figure can exceed 1, so a chance of 1.01 has to clear a threshold
/// of 1.0 — which is exactly what routing either side through
/// <see cref="Figures.Fraction"/> would prevent.
/// </para>
/// </summary>
public static class OfferAcceptance
{
    /// <summary>
    /// Whether a chance is good enough for the host. "At least this likely", so
    /// a chance sitting exactly on the threshold clears it.
    /// </summary>
    public static bool ClearsThreshold(float successChance, float threshold) =>
        successChance >= threshold;

    /// <summary>
    /// Build the search's probe from the game's success chance.
    /// </summary>
    /// <param name="successChance">
    /// What the game gives for a deal of that many units at that total price —
    /// in the mod, <c>Customer.GetOfferSuccessChance</c>, whose price argument
    /// is the whole deal because the quantity rides in its item list.
    /// </param>
    /// <param name="threshold">
    /// The confidence to draw the whole curve at — a rung of
    /// <see cref="ConfidenceSearch.Rungs"/>, not the player's floor.
    /// </param>
    public static OfferProbe Probe(Func<int, float, float> successChance, float threshold)
    {
        if (successChance is null)
        {
            throw new ArgumentNullException(nameof(successChance));
        }

        return (quantity, totalPrice) =>
            ClearsThreshold(successChance(quantity, totalPrice), threshold);
    }
}
