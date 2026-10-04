using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;

namespace Dealcraft;

/// <summary>
/// Reads the two figures a negotiation needs off the game and hands them to
/// <see cref="NegotiationSearch"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is all that is left on the game's side of the seam, and it is the whole
/// of what belongs there: the player's listed price for this product, and a
/// function that asks this customer how likely they are to take a deal. The
/// search itself — the curve at every confidence, the offer the screen would
/// hold, the rung that pays most — is pure and is in <c>Dealcraft.Core</c>, where
/// a scenario can drive it without a game.
/// </para>
/// <para>
/// The floor is not a setting and never was a choice. It is what the player
/// lists <em>this</em> product at — <c>ProductManager.GetPrice(definition)</c>,
/// read here because it is a different figure for every product — and it is the
/// floor for everybody, always. The offer on the table was generated from that
/// same number, so a counter under it would
/// undercut the price the player set themselves.
/// </para>
/// <para>
/// The probe is <c>Customer.GetOfferSuccessChance</c> and never
/// <c>EvaluateCounteroffer</c>, which folds a <c>Random.Range</c> into its
/// answer. See <see cref="OfferChanceProbe"/>.
/// </para>
/// </remarks>
internal static class CounterofferSearch
{
    /// <summary>
    /// Examine the price points this search covers and pick one.
    /// </summary>
    /// <param name="customer">Who is being asked.</param>
    /// <param name="product">What they are being asked about.</param>
    /// <param name="limits">What the counteroffer screen will hold.</param>
    /// <param name="quantityOnScreen">
    /// The quantity currently on the table — the screen's, or the customer's
    /// own offered quantity when no screen is open. It is what gets priced.
    /// </param>
    /// <param name="diagnosis">
    /// What the probing came to, for the record to write out. Handed back rather
    /// than logged from in here: one search is a few dozen probes, and the whole
    /// point of the accumulator is that they cost one line between them.
    /// </param>
    public static Negotiation For(
        Customer customer,
        ProductDefinition product,
        in CounterofferLimits limits,
        int quantityOnScreen,
        float chanceFloor,
        in OfferedContract offered,
        out ProbeDiagnosis diagnosis)
    {
        var probe = new OfferChanceProbe(customer, product);
        ProbeDiagnosis record = probe.Diagnosis;
        diagnosis = record;

        // The method that actually decides, worked out rather than probed.
        //
        // The search has always run over Customer.GetOfferSuccessChance because
        // Customer.EvaluateCounteroffer folds a roll into the bool it returns.
        // That was true and it was the wrong conclusion: the roll is uniform, so
        // the chance of clearing it is arithmetic, and it is the chance the
        // player's confidence setting was always meant to be a floor on.
        //
        // The probe stays, asked once for whatever the search settles on, so the
        // record carries both figures. Whether the two functions agree is the
        // open question this leaves behind, and the next session answers it.
        if (CounterofferFactsReader.TryRead(
                customer, product, offered, out CounterofferFacts facts, out string trouble))
        {
            // Every reckoning is written down, the way every probe used to be.
            //
            // Moving the search off the probe took the numbers out of the log
            // with it: four of the owner's negotiations then read "no price at
            // any quantity reached the confidence the search asked for" and
            // nothing else, where the day before the same refusal had carried
            // the price, the chance and the count. A refusal without figures
            // cannot be argued with, and this whole feature is decided by
            // arguing with refusals.
            float Reckon(int quantity, float totalPrice)
            {
                float chance = CounterofferOdds.Of(facts, quantity, totalPrice);
                record.Answered(quantity, totalPrice, chance);
                return chance;
            }

            Negotiation reckoned = NegotiationSearch.For(
                limits,
                quantityOnScreen,
                ProductValuationReader.AskingPrice(product),
                chanceFloor,
                Reckon);

            if (reckoned.Planned.HasOffer)
            {
                probe.Chance(reckoned.Planned.Quantity, reckoned.Planned.TotalPrice);
            }

            // What the reckoning was built on, so a refusal can be checked
            // rather than taken. The listed price and the market value are the
            // two the negotiation is caught between: the search may not go below
            // the first, and the game judges every counter against the second.
            record.Failed(
                null,
                $"they offered {facts.OfferedQuantity} for ${facts.OfferedPayment:0.##}, "
                + $"the market value is ${facts.MarketValue:0.##} a unit, your listed price is "
                + $"${ProductValuationReader.AskingPrice(product):0.##}, and one order of theirs "
                + $"is worth ${facts.BudgetPerOrder:0.##}");

            return reckoned;
        }

        // Nothing readable, so nothing is reckoned. The probe is not a fallback:
        // it answers a different question, and answering the wrong one quietly
        // is what this change exists to stop.
        diagnosis.Failed(null, $"the counter-offer facts could not be read ({trouble})");

        return Negotiation.Nothing($"the counter-offer facts could not be read ({trouble})");
    }
}
