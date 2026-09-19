using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Builds the quantity/price curve by asking the customer. Acceptance is
/// monotone in price, so each quantity's boundary is found exactly by bisection
/// in a bounded number of probes.
/// </summary>
public static class PriceCurveSearch
{
    /// <summary>
    /// Build the curve and pick a point off it in one go. The bounds are the
    /// whole of what may be searched: there is no preference that narrows them,
    /// because there is no deal-size goal left to narrow them for.
    /// </summary>
    /// <param name="floorPerUnit">
    /// The price the player lists this product at. See
    /// <see cref="PriceCurvePolicy.Choose"/>.
    /// </param>
    public static CurveChoice BestOffer(
        in OfferBounds bounds,
        OfferProbe probe,
        float floorPerUnit) =>
        PriceCurvePolicy.Choose(Build(bounds, probe), floorPerUnit);

    /// <summary>
    /// Probe every quantity the bounds allow and record the highest total the
    /// customer accepts for each, until the quantities run out or
    /// <see cref="OfferBounds.MaxProbes"/> does.
    /// </summary>
    public static PriceCurve Build(in OfferBounds bounds, OfferProbe probe)
    {
        if (probe is null)
        {
            throw new ArgumentNullException(nameof(probe));
        }

        // Each point costs at least one probe, so the budget caps how many
        // there can be however wide the quantity range is.
        int quantities = bounds.MaxQuantity - bounds.MinQuantity + 1;
        var points = new List<PricePoint>(Math.Min(quantities, bounds.MaxProbes));

        int budget = bounds.MaxProbes;
        int probes = 0;
        bool exhausted = false;

        for (int quantity = bounds.MinQuantity; quantity <= bounds.MaxQuantity; quantity++)
        {
            if (budget == 0)
            {
                exhausted = true;
                break;
            }

            PricePoint point = HighestAcceptedTotal(quantity, bounds, probe, ref budget, out bool cutShort);
            probes += point.Probes;
            points.Add(point);

            if (cutShort)
            {
                exhausted = true;
                break;
            }
        }

        return new PriceCurve(points, probes, exhausted);
    }

    /// <summary>
    /// Bisect the price grid for one quantity. The returned price was observed
    /// accepted, never merely inferred, which is what keeps the answer sane for
    /// a customer whose acceptance is not in fact monotone — and what makes it
    /// safe to stop early when the budget runs out.
    /// </summary>
    /// <param name="budget">
    /// Probes left for the whole curve. Spent down as this quantity is probed.
    /// The caller guarantees at least one.
    /// </param>
    /// <param name="cutShort">
    /// True when the budget ran out with bisecting still to do. The price
    /// returned is then one the customer was seen to accept but not necessarily
    /// the highest such price.
    /// </param>
    private static PricePoint HighestAcceptedTotal(
        int quantity,
        OfferBounds bounds,
        OfferProbe probe,
        ref int budget,
        out bool cutShort)
    {
        long steps = PriceSteps(bounds);
        int probes = 0;
        int left = budget;
        bool stopped = false;

        float PriceAt(long index)
        {
            double price = bounds.MinTotalPrice + (index * (double)bounds.PriceResolution);
            return price >= bounds.MaxTotalPrice ? bounds.MaxTotalPrice : (float)price;
        }

        bool Ask(long index)
        {
            left--;
            probes++;
            return probe(quantity, PriceAt(index));
        }

        // The bisection returns from several places; spending the budget is
        // recorded once, after it, so no exit can forget to.
        PricePoint Bisect()
        {
            // The top of the range first: a customer who takes it needs one probe.
            if (Ask(steps))
            {
                return new PricePoint(quantity, accepted: true, bounds.MaxTotalPrice, probes);
            }

            if (steps == 0)
            {
                return new PricePoint(quantity, accepted: false, 0f, probes);
            }

            if (left == 0)
            {
                stopped = true;
                return new PricePoint(quantity, accepted: false, 0f, probes);
            }

            if (!Ask(0))
            {
                return new PricePoint(quantity, accepted: false, 0f, probes);
            }

            long accepted = 0;
            long refused = steps;

            while (refused - accepted > 1)
            {
                if (left == 0)
                {
                    stopped = true;
                    break;
                }

                long middle = accepted + ((refused - accepted) / 2);
                if (Ask(middle))
                {
                    accepted = middle;
                }
                else
                {
                    refused = middle;
                }
            }

            return new PricePoint(quantity, accepted: true, PriceAt(accepted), probes);
        }

        PricePoint point = Bisect();
        budget = left;
        cutShort = stopped;
        return point;
    }

    /// <summary>
    /// How many resolution steps fit in the price range. The last step lands on
    /// the maximum exactly, so the ceiling is always probed as given.
    /// <see cref="OfferBounds"/> guarantees the count is addressable.
    /// </summary>
    private static long PriceSteps(in OfferBounds bounds)
    {
        double span = bounds.MaxTotalPrice - (double)bounds.MinTotalPrice;
        return (long)Math.Ceiling(span / bounds.PriceResolution);
    }
}
