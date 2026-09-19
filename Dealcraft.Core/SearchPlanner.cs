using System;

namespace Dealcraft.Core;

/// <summary>
/// Turns the limits a negotiation screen imposes into bounds the curve search
/// can work in, choosing a price step coarse enough that the whole curve stays
/// inside a stated number of price points examined.
/// </summary>
/// <remarks>
/// Every probe is a call into the live game, so the budget is the first-class
/// input here and the resolution follows from it — not the other way round. A
/// finer grid is always available for the asking; it is only ever paid for in
/// probes.
///
/// <para>A probe is <c>Customer.GetOfferSuccessChance</c>, which reads the
/// customer in memory and returns a probability. It sends no message, makes no
/// RPC and rolls no dice, so the budget is a cost in method calls and nothing
/// else. In particular it is <b>not</b> a count of counteroffers: the player
/// gets exactly one of those, and spending it is
/// <c>Customer.SendCounteroffer</c>, which clears the standing offer's
/// responses. See the project notes.</para>
/// </remarks>
public static class SearchPlanner
{
    /// <summary>
    /// The most price points one curve may examine.
    /// </summary>
    /// <remarks>
    /// This was <c>MaxProbesPerNegotiation</c> in the preferences file, and it
    /// is a constant now because it was never a decision about a business: it is
    /// a performance knob, and it is the one figure that could switch a feature
    /// off by accident — a budget too small for the price range on screen makes
    /// every curve impossible. A probe is <c>Customer.GetOfferSuccessChance</c>,
    /// which reads the customer in memory and sends nothing, so the cost is
    /// method calls and nothing else.
    /// </remarks>
    public const int ProbeBudget = 200;

    /// <summary>
    /// Price steps a dealer would recognise. A recommendation of $1,350 reads
    /// as a decision; $1,347.91 reads as a machine talking.
    /// </summary>
    private static readonly int[] NiceMantissas = { 1, 2, 5 };

    /// <summary>
    /// The finest step worth searching. The game's own money display rounds to
    /// whole units, so a finer grid would cost probes to find a difference the
    /// player never sees.
    /// </summary>
    private const float FinestStep = 1f;

    /// <summary>
    /// A grid finer than this would exhaust the step search long before the
    /// probe budget ran out, and no real price range needs it.
    /// </summary>
    private const int MostSteps = 1 << 20;

    public static SearchPlan For(
        int minQuantity,
        int maxQuantity,
        float minPrice,
        float maxPrice,
        int probeBudget)
    {
        int lowestQuantity = Math.Max(1, minQuantity);
        if (maxQuantity < lowestQuantity)
        {
            return SearchPlan.Impossible(
                $"the screen allows no quantity between {lowestQuantity} and {maxQuantity}");
        }

        float floor = Math.Max(0f, minPrice);
        if (maxPrice < floor)
        {
            return SearchPlan.Impossible(
                $"the screen allows no price between {floor:0.##} and {maxPrice:0.##}");
        }

        int quantities = maxQuantity - lowestQuantity + 1;
        int perQuantity = probeBudget / quantities;

        // Two probes per quantity — the top of the range and the bottom — is
        // the least that can establish anything at all.
        if (perQuantity < 2)
        {
            return SearchPlan.Impossible(
                $"{quantities} quantities cannot be searched within {probeBudget} price points");
        }

        int affordableSteps = AffordableSteps(perQuantity);
        float resolution = StepFor(maxPrice - floor, affordableSteps);
        var bounds = new OfferBounds(lowestQuantity, maxQuantity, floor, maxPrice, resolution);

        return SearchPlan.Searchable(bounds, quantities * ProbesPerQuantity(StepsIn(bounds)));
    }

    /// <summary>
    /// How many price steps a per-quantity allowance buys. The bisection spends
    /// two probes on the ends of the range and then halves what is left, so
    /// the allowance is an exponent.
    /// </summary>
    private static int AffordableSteps(int perQuantity)
    {
        int exponent = perQuantity - 2;
        return exponent >= 20 ? MostSteps : 1 << exponent;
    }

    /// <summary>
    /// The coarsest step a dealer would recognise that still splits the range
    /// into no more than <paramref name="steps"/> parts.
    /// </summary>
    private static float StepFor(float range, int steps)
    {
        float needed = range / steps;

        // A short-circuit, not a decision: the ladder's first rung is 1, and
        // `1 >= needed` holds for every needed at or under it, so the loop below
        // would answer the same. Said here because mutation testing flags this
        // line as reachable-but-unkillable every time, and it is worth knowing
        // that is the reason rather than a missing test.
        if (needed <= FinestStep)
        {
            return FinestStep;
        }

        for (long scale = 1; scale <= 1_000_000_000L; scale *= 10)
        {
            foreach (int mantissa in NiceMantissas)
            {
                float candidate = mantissa * (float)scale;
                if (candidate >= needed)
                {
                    return candidate;
                }
            }
        }

        // A range this wide is not a price; take it in one step rather than
        // pretending to search it.
        return range;
    }

    /// <summary>
    /// How many steps of the bounds' own resolution fit in its price range.
    /// Mirrors what the search itself will do with these bounds.
    /// </summary>
    private static int StepsIn(in OfferBounds bounds)
    {
        double span = bounds.MaxTotalPrice - (double)bounds.MinTotalPrice;
        return (int)Math.Ceiling(span / bounds.PriceResolution);
    }

    /// <summary>
    /// The most probes one quantity's bisection can take: the two ends of the
    /// range, then one per halving of what lies between them.
    /// </summary>
    private static int ProbesPerQuantity(int steps) => steps == 0 ? 1 : 2 + CeilLog2(steps);

    private static int CeilLog2(int value)
    {
        int bits = 0;
        int remaining = value - 1;
        while (remaining > 0)
        {
            bits++;
            remaining >>= 1;
        }

        return bits;
    }
}
