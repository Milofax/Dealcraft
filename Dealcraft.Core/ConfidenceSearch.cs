using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// One rung of the confidence search: what the curve found at that confidence,
/// and what the customer's own chance of taking it turned out to be.
/// </summary>
/// <param name="Confidence">The rung the curve was drawn at.</param>
/// <param name="Offer">What the curve found there, as the screen would hold it.</param>
/// <param name="Chance">
/// <c>Customer.GetOfferSuccessChance</c> for that exact offer — read for the
/// offer that will be made, not for the rung it was searched at, because the
/// screen rounds both numbers.
/// </param>
public readonly record struct ConfidenceAttempt(float Confidence, PlannedOffer Offer, float Chance);

/// <summary>
/// The rung that pays best, and what it is worth.
/// </summary>
public readonly struct BestOffer
{
    private readonly PlannedOffer offer;

    internal BestOffer(float confidence, PlannedOffer offer, float chance, bool found)
    {
        Confidence = confidence;
        this.offer = offer;
        Chance = chance;
        Found = found;
    }

    /// <summary>Whether any rung found an offer at all.</summary>
    public bool Found { get; }

    /// <summary>The confidence the winning curve was drawn at.</summary>
    public float Confidence { get; }

    /// <summary>The offer to make, or the refusal and its reason.</summary>
    public PlannedOffer Offer => offer;

    /// <summary>The game's own chance that the customer takes it.</summary>
    public float Chance { get; }

    /// <summary>
    /// What the bet is worth: the chance times the money. This is the number
    /// <see cref="CounterofferGate.Send"/> decides on.
    /// </summary>
    public float Worth => Found ? Figures.Fraction(Chance) * offer.TotalPrice : 0f;
}

/// <summary>
/// Picks the confidence the curve is drawn at, per offer, instead of taking it
/// from a setting.
/// </summary>
/// <remarks>
/// <para>
/// The curve at confidence <c>t</c> finds the highest price the customer clears
/// with chance at least <c>t</c>. What that offer is worth is
/// <c>chance × price</c> — and the <c>t</c> that maximises it is a property of
/// <em>this customer's own curve</em>: steep for one, flat for another. There is
/// no value of <c>t</c> that is right for everyone, so it was never the player's
/// number to choose.
/// </para>
/// <para>
/// <c>AcceptanceProbabilityThreshold</c> stopped being that number because of
/// it. It is on the app as <see cref="ChanceFloor"/> — a floor on what this
/// search returns, applied afterwards by <see cref="CounterofferGate.Send"/> —
/// and nothing about it enters the ranking below.
/// </para>
/// <para>
/// It is affordable because a probe costs nothing that matters.
/// <c>Customer.GetOfferSuccessChance</c> is a deterministic local read — no
/// <c>Random</c>, no <c>Message</c>, no RPC, no writes; see
/// <c>docs/counteroffer-truth.md</c>. The whole reason a fixed threshold existed
/// was to keep the probe count down, and that cost is not real. Seven rungs at
/// one quantity is seven bisections, about ninety lookups against a customer
/// already in memory.
/// </para>
/// <para>
/// Pure: the caller builds each rung's curve and reads each chance, and this
/// only says which one won.
/// </para>
/// </remarks>
public static class ConfidenceSearch
{
    /// <summary>
    /// The confidences worth drawing a curve at. Below a half the advisor would
    /// be settling for a coin toss.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These used to live in <c>AutomationCatalog</c> as the ladder a player
    /// stepped through. They are here now because this is the only thing left
    /// that reads them.
    /// </para>
    /// <para>
    /// <b>The top rung is <c>1.0</c> and it was <c>0.95</c>.</b> The ladder is
    /// what the winning offer's chance is read off, and
    /// <see cref="ChanceFloor"/> lets a player refuse anything under 100% — so a
    /// ladder stopping at 0.95 would make the top of their control mean "never
    /// counter at all" rather than "only when it is certain". A rung whose curve
    /// finds nothing costs one bisection and is passed over by
    /// <see cref="Best"/>, which is what the old comment about 0.95 rarely
    /// reaching the asking price was really describing.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<float> Rungs { get; } =
        new[] { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 0.95f, 1f };

    /// <summary>
    /// The rung worth most, by <c>chance × price</c>.
    /// </summary>
    /// <param name="tried">
    /// What each rung found, in any order. A rung that found nothing is passed
    /// over rather than counted as worth nothing, so its reason can still be the
    /// one reported when they all refuse.
    /// </param>
    public static BestOffer Best(IReadOnlyList<ConfidenceAttempt> tried)
    {
        var best = new BestOffer(0f, PlannedOffer.None("no confidence was searched"), 0f, found: false);
        float most = 0f;

        foreach (ConfidenceAttempt attempt in tried)
        {
            if (!attempt.Offer.HasOffer)
            {
                continue;
            }

            float worth = Figures.Fraction(attempt.Chance) * attempt.Offer.TotalPrice;
            if (best.Found && worth <= most)
            {
                continue;
            }

            most = worth;
            best = new BestOffer(attempt.Confidence, attempt.Offer, attempt.Chance, found: true);
        }

        return best.Found ? best : Refused(tried);
    }

    /// <summary>
    /// Why every rung refused, in the words of the rung most likely to have
    /// found something — the lowest confidence tried, because it asked the
    /// customer for the least.
    /// </summary>
    private static BestOffer Refused(IReadOnlyList<ConfidenceAttempt> tried)
    {
        var reason = PlannedOffer.None("no confidence was searched");
        float lowest = float.MaxValue;

        foreach (ConfidenceAttempt attempt in tried)
        {
            if (attempt.Confidence < lowest)
            {
                lowest = attempt.Confidence;
                reason = attempt.Offer;
            }
        }

        return new BestOffer(0f, reason, 0f, found: false);
    }
}
