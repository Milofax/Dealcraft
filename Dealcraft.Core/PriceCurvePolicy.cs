namespace Dealcraft.Core;

/// <summary>
/// Picks a point off the curve. One rule, no modes: strike out every quantity
/// priced under the floor and take the highest total that remains.
/// </summary>
public static class PriceCurvePolicy
{
    /// <param name="floorPerUnit">
    /// The price the player lists this product at, per unit —
    /// <c>ProductManager.GetPrice(definition)</c>, which is what
    /// <c>Customer.TryGenerateContract</c> built the incoming offer from
    ///. It is the floor for everybody,
    /// always, and it is not a switch: there is no host figure that outranks it
    /// and no preference that turns it off. Zero only where the game has no
    /// price to give — before a save is loaded — and the curve is then unbounded
    /// below rather than refusing everything.
    /// </param>
    public static CurveChoice Choose(PriceCurve curve, float floorPerUnit)
    {
        int quantity = 0;
        float asked = 0f;
        PricePoint? bestAvailable = null;

        foreach (PricePoint point in curve.Points)
        {
            if (!point.Accepted)
            {
                continue;
            }

            if (bestAvailable is null || point.PricePerUnit > bestAvailable.Value.PricePerUnit)
            {
                bestAvailable = point;
            }

            // There is no ceiling any more. MaximumPricePerUnit was file-only,
            // defaulted to off, and only ever asked less than the game said the
            // customer would pay — so it strictly reduced income, and one set
            // under the listed price killed counter-offers in silence.
            float ask = point.TotalPrice;
            if (floorPerUnit > 0f && ask / point.Quantity < floorPerUnit)
            {
                continue;
            }

            if (ask > asked)
            {
                quantity = point.Quantity;
                asked = ask;
            }
        }

        // A curve cut short is still a real curve; the caller is told so it can
        // say the answer is the best of a partial look rather than the best.
        string truncated = curve.BudgetExhausted
            ? $" (the probe budget ran out after {curve.Probes}, so the curve is partial)"
            : string.Empty;

        if (quantity == 0)
        {
            if (bestAvailable is not PricePoint available)
            {
                // The silence this sentence has to survive: the player may have
                // taken this customer's whole week already, and would then want
                // to wait rather than to drop the price. The curve cannot tell
                // him which it is. `GetOfferSuccessChance` weighs an offer
                // against `GetAdjustedWeeklySpend`, a weekly *capacity* that
                // reads no purchase history and does not fall as the week is
                // spent (docs/counteroffer-truth.md). So the sentence reports
                // what was asked and what came back, names the figure that is
                // not in the answer, and sends him to the one place that does
                // carry it — "Spent this week", on the customer's own card.
                return CurveChoice.Abstain(
                    "no price at any quantity reached the confidence the search asked for; "
                    + "what this customer has already spent this week is not part of that "
                    + $"answer, so check the week before lowering the price{truncated}",
                    curve.Probes,
                    probeBudgetExhausted: curve.BudgetExhausted);
            }

            // The floor is the only thing that can strike a quantity out, so
            // this sentence is the whole of why there is no offer. The block
            // header reads "nothing clears your floor" off it.
            //
            // Nothing is claimed here beyond the two prices: the floor that
            // struck every quantity out, the highest per-unit price the curve
            // did clear, and the one move that would take it. Whether the week
            // is already spent does not enter — it cannot, see the branch above.
            return CurveChoice.Abstain(
                $"nothing clears your floor of {floorPerUnit:0.##} per unit; " +
                $"the highest the curve cleared was {available.PricePerUnit:0.##} per unit " +
                $"for {available.Quantity}, so lower the listed price to " +
                $"{available.PricePerUnit:0.##} to take it{truncated}",
                curve.Probes,
                available,
                curve.BudgetExhausted);
        }

        return CurveChoice.Offer(
            quantity,
            asked,
            curve.Probes,
            $"best of {curve.Points.Count} quantities at {asked / quantity:0.##} per unit{truncated}",
            curve.BudgetExhausted);
    }
}
