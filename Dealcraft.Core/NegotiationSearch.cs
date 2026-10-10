using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// What one search of the price curve came to: the point it picked, the offer
/// that makes on the counteroffer screen, and how likely the customer is to take
/// it.
/// </summary>
public readonly struct Negotiation
{
    private Negotiation(
        CurveChoice recommendation,
        PlannedOffer planned,
        float chance,
        float confidence,
        int asked,
        string explanation)
    {
        Recommendation = recommendation;
        Planned = planned;
        Chance = chance;
        Confidence = confidence;
        Asked = asked;
        Explanation = explanation;
    }

    /// <summary>The raw point off the curve, before the screen's limits.</summary>
    public CurveChoice Recommendation { get; }

    /// <summary>
    /// What to offer: the recommendation as the screen's own controls would hold
    /// it. This is what the automation sends.
    /// </summary>
    public PlannedOffer Planned { get; }

    /// <summary>
    /// The game's own chance that the customer takes the planned offer, 0..1.
    /// Read for the offer that will actually be made, not for the curve point
    /// before the screen rounded it. Never clamped: the game's figure can exceed
    /// one, and <see cref="CounterofferGate.Send"/> compares it against a floor
    /// that reaches one.
    /// </summary>
    public float Chance { get; }

    /// <summary>The rung the winning curve was drawn at. Zero when none won.</summary>
    public float Confidence { get; }

    /// <summary>How many price points the customer was asked about.</summary>
    public int Asked { get; }

    /// <summary>One line for the log: what was asked, and what it cost.</summary>
    public string Explanation { get; }

    public static Negotiation Nothing(string reason) => new(
        CurveChoice.Abstain(reason), PlannedOffer.None(reason), 0f, 0f, 0, reason);

    internal static Negotiation Found(
        CurveChoice recommendation,
        in BestOffer best,
        int asked,
        string explanation) =>
        new(recommendation, best.Offer, best.Chance, best.Confidence, asked, explanation);
}

/// <summary>
/// Builds the price curve for one customer and one product at every confidence
/// worth asking for, and turns whichever pays best into the offer to make.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the whole of the negotiation decision and it is pure.</b> It knows
/// the screen's limits as four numbers, the quantity on the table as an integer,
/// the player's listed price as a float, and the customer as a function from a
/// deal to a probability. It knows nothing about a screen, a customer object or
/// a product definition.
/// </para>
/// <para>
/// It lived in <c>Dealcraft.Mod/CounterofferSearch</c>, on the game's side of the
/// seam, purely because that is where the two readers it needs are — the listed
/// price off <c>ProductManager.GetPrice</c> and the chance off
/// <c>Customer.GetOfferSuccessChance</c>. Neither of those is in here; both are
/// handed in as the plain values they already were. The adapter is what is left
/// over: two reads and this call.
/// </para>
/// <para>
/// The confidence used to be a setting. It is not the player's number to choose:
/// the curve at confidence <c>t</c> finds the highest price the customer clears
/// with chance at least <c>t</c>, what that is worth is <c>chance × price</c>,
/// and the <c>t</c> that maximises it is a property of <em>this customer's</em>
/// curve — steep for one, flat for another. The player's number is
/// <see cref="ChanceFloor"/>, and nothing here reads it: it is a floor on what
/// this returns, applied by <see cref="CounterofferGate.Send"/> once the winner
/// is known, so the search chooses exactly as it would if the player had never
/// set one.
/// </para>
/// <para>
/// It is affordable because a probe costs nothing that matters.
/// <c>Customer.GetOfferSuccessChance</c> is a deterministic local read — no
/// <c>Random</c>, no <c>Message</c>, no RPC, no writes; see
/// the project notes. One rung at one quantity is one bisection
/// against a customer already in memory, and the customer is not addressed once.
/// The probe is never <c>Customer.EvaluateCounteroffer</c>, which folds a
/// <c>Random.Range</c> into its answer, so bisecting it finds noise rather than a
/// boundary.
/// </para>
/// </remarks>
public static class NegotiationSearch
{
    /// <summary>
    /// Examine the price points this search covers and pick one.
    /// </summary>
    /// <param name="limits">What the counteroffer screen will hold.</param>
    /// <param name="quantityOnScreen">
    /// The quantity currently on the table — the screen's, or the customer's own
    /// offered quantity when no screen is open. It is what gets priced.
    /// </param>
    /// <param name="floorPerUnit">
    /// What the player lists <em>this</em> product at, per unit. It is the floor
    /// for everybody, always, and it is not a switch: the offer on the table was
    /// generated from that same number, so a
    /// counter under it would undercut the price the player set themselves.
    /// </param>
    /// <param name="chance">
    /// The game's own chance that this customer takes so many units for so much
    /// money, 0..1 — <c>Customer.GetOfferSuccessChance</c>, whose price argument
    /// is the whole deal because the quantity rides in its item list.
    /// </param>
    public static Negotiation For(
        in CounterofferLimits limits,
        int quantityOnScreen,
        float floorPerUnit,
        float chanceFloor,
        Func<int, float, float> chance)
    {
        if (chance is null)
        {
            throw new ArgumentNullException(nameof(chance));
        }

        QuantityRange search = CounterofferPlan.Search(limits, quantityOnScreen);

        if (!search.IsViable)
        {
            return Negotiation.Nothing(limits.Problem);
        }

        SearchPlan plan = SearchPlanner.For(
            search.Lowest,
            search.Highest,
            limits.MinPrice,
            limits.MaxPrice,
            SearchPlanner.ProbeBudget);

        if (!plan.IsViable)
        {
            return Negotiation.Nothing(plan.Problem);
        }

        int asked = 0;

        float Ask(int quantity, float totalPrice)
        {
            asked++;
            return chance(quantity, totalPrice);
        }

        BestOffer best = Searched(plan, limits, floorPerUnit, chanceFloor, Ask, out CurveChoice recommendation);

        return Negotiation.Found(recommendation, best, asked, Explain(best, search, plan, asked));
    }

    /// <summary>
    /// Draw the curve at every confidence worth asking for and keep whichever
    /// pays best.
    /// </summary>
    private static BestOffer Searched(
        in SearchPlan plan,
        in CounterofferLimits limits,
        float floorPerUnit,
        float chanceFloor,
        Func<int, float, float> chance,
        out CurveChoice chosen)
    {
        var tried = new List<ConfidenceAttempt>(ConfidenceSearch.Rungs.Count);
        var curves = new List<CurveChoice>(ConfidenceSearch.Rungs.Count);

        foreach (float rung in ConfidenceSearch.Rungs)
        {
            // OfferAcceptance owns the comparison, so the curve's verdict and
            // the readout's verdict stay the same rule at every rung.
            CurveChoice curve = PriceCurveSearch.BestOffer(
                plan.Bounds, OfferAcceptance.Probe(chance, rung), floorPerUnit);

            PlannedOffer planned = CounterofferPlan.Offer(curve, limits);

            // Read for the offer that will actually be made, not for the curve
            // point before the screen's controls rounded it.
            float taken = planned.HasOffer ? chance(planned.Quantity, planned.TotalPrice) : 0f;

            curves.Add(curve);
            tried.Add(new ConfidenceAttempt(rung, planned, taken));
        }

        BestOffer best = ConfidenceSearch.Best(tried, chanceFloor);

        chosen = CurveChoice.Abstain(best.Offer.Reason);
        for (int i = 0; i < tried.Count; i++)
        {
            if (best.Found && tried[i].Confidence == best.Confidence)
            {
                chosen = curves[i];
                break;
            }
        }

        return best;
    }

    /// <summary>
    /// One line for the log.
    /// </summary>
    /// <remarks>
    /// <b>Price points, not questions.</b> A probe is a local lookup and the
    /// customer is never addressed; the one thing Dealcraft spends is the single
    /// counteroffer. This string used to say "questions", which read as the mod
    /// pestering somebody who may only be asked once — the same class of defect
    /// as a label promising more than the code does, in a string the owner could
    /// see.
    /// </remarks>
    private static string Explain(
        in BestOffer best,
        in QuantityRange search,
        in SearchPlan plan,
        int asked)
    {
        int budget = (plan.WorstCaseProbes + 1) * ConfidenceSearch.Rungs.Count;

        return $"curve over {search} units at {ConfidenceSearch.Rungs.Count} confidences: "
            + $"{best.Offer} ({asked} price points of at most "
            + $"{budget.ToString(CultureInfo.InvariantCulture)}, best at "
            + $"{Percent(best.Confidence)} confidence and taken at {Percent(best.Chance)}, "
            + $"price step {plan.Bounds.PriceResolution.ToString("0.##", CultureInfo.InvariantCulture)})";
    }

    /// <summary>
    /// Whole percent, unclamped — <see cref="Figures.Percent"/> holds a figure
    /// inside 0..1, and the chance this spells is the game's own, which can
    /// exceed it. A line reading 101% is the line telling the truth.
    /// </summary>
    private static string Percent(float fraction) =>
        Math.Round(fraction * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
}
