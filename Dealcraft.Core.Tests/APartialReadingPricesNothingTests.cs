using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// A reading that could not take every customer prices nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// This is the guard for the worst thing this project has done to the owner's
/// save. On 2026-09-19 every customer was refused for an unreadable order size,
/// the pass carried on with whoever was left, and over three passes it wrote
/// <c>$160 → $2 → $3 → $4</c> for one product and <c>$280 → $7 → $12 → $14</c>
/// for another. The game builds its offers from the listed price, so his
/// customers then began asking for forty-five units at a few hundred dollars,
/// and nothing about a written price can be undone.
/// </para>
/// <para>
/// The behaviour it replaces was itself a fix: before it, one refused customer
/// took the whole product's reading down with it, and the pass never completed.
/// Turning that into "step over the customer and carry on" made the pass
/// complete and made it dangerous in the same change. What was missing is that
/// a partial reading does not give a smaller truth — it gives the best price
/// for a roster that does not exist.
/// </para>
/// </remarks>
public class APartialReadingPricesNothingTests
{
    [Fact]
    public void A_reading_that_stepped_over_a_customer_is_not_complete()
    {
        var valuation = new ProductValuation { Available = true, ProductId = "ogkush" };
        valuation.Candidates.Add(new ProductCandidate("Beth Penn", 8, 760f, 0.9f));

        Assert.True(valuation.IsComplete);

        valuation.Notes.Add("Geraldine Poon (no dealer): the game answered with an order of "
            + "2147483647 units of one product, and its own limit is 1000");

        Assert.False(valuation.IsComplete);
    }

    /// <summary>
    /// And one is enough. The owner's own case had the whole roster refused, but
    /// the rule must not be a proportion — a single customer missing is a
    /// different roster, and the price that falls out of it is a price for that
    /// different roster.
    /// </summary>
    [Fact]
    public void One_stepped_over_customer_is_enough_to_hold_the_whole_product()
    {
        var valuation = new ProductValuation { Available = true, ProductId = "ogkush" };

        for (int i = 0; i < 30; i++)
        {
            valuation.Candidates.Add(new ProductCandidate($"Customer {i}", 5, 400f, 0.8f));
        }

        valuation.Notes.Add("Marco Barone (no dealer): the game answered with an order of "
            + "2147483647 units of one product, and its own limit is 1000");

        Assert.False(valuation.IsComplete);
    }

    /// <summary>
    /// A reading nobody could order from is complete and prices nothing, and
    /// that is a different answer from an incomplete one: the first is a fact
    /// about the product, the second is a fact about the reading.
    /// </summary>
    [Fact]
    public void A_product_nobody_can_order_is_still_a_complete_reading()
    {
        var valuation = new ProductValuation { Available = true, ProductId = "ogkush" };

        Assert.True(valuation.IsComplete);
        Assert.Empty(valuation.Candidates);
    }

    /// <summary>
    /// The adapter asks before it prices, checked where it can be: the pass is
    /// an Il2Cpp file no test can run, so the guard is that the call is there
    /// and that it comes before the recommendation is worked out.
    /// </summary>
    [Fact]
    public void The_pass_checks_the_reading_before_it_works_out_a_price()
    {
        string source = AdapterSource.Read("ListedPricePass.cs");

        int checkedAt = source.IndexOf("!valuation.IsComplete", System.StringComparison.Ordinal);

        // The call, with its argument: the class is named in this file's own
        // remarks several lines above anything that runs.
        int pricedAt = source.IndexOf(
            "PricingRecommendation.Best(valuation.Candidates", System.StringComparison.Ordinal);

        Assert.True(checkedAt >= 0, "ListedPricePass no longer asks whether the reading is complete.");
        Assert.True(
            checkedAt < pricedAt,
            "ListedPricePass must refuse an incomplete reading before it works out a price, not "
            + "after: a price worked out is a price one edit away from being written.");
    }
}
