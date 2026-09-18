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
/// same number (<c>docs/listed-price-truth.md</c>), so a counter under it would
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
        out ProbeDiagnosis diagnosis)
    {
        var probe = new OfferChanceProbe(customer, product);
        diagnosis = probe.Diagnosis;

        return NegotiationSearch.For(
            limits,
            quantityOnScreen,
            ProductValuationReader.AskingPrice(product),
            probe.Chance);
    }
}
