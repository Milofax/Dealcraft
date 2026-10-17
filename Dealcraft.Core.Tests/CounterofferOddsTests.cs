using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The chance the game takes a counter-offer, worked out rather than probed.
/// </summary>
/// <remarks>
/// The figures come from a reading of the game's own decision, checked against
/// two things players had already noticed and could not explain. Nobody has
/// watched a counter-offer go out under it, so these hold the arithmetic to the
/// reading rather than the reading to the game.
/// </remarks>
public class CounterofferOddsTests
{
    /// <summary>A customer who likes the product and is asked a fair price.</summary>
    private static CounterofferFacts Ordinary(float offeredPayment = 100f, int quantity = 4) =>
        new(
            marketValue: 25f,
            offeredQuantity: quantity,
            offeredPayment: offeredPayment,
            enjoyment: 0.5f,
            addiction: 0.2f,
            normalisedRelationship: 0.4f,
            budgetPerOrder: 200f);

    [Fact]
    public void A_total_at_three_times_their_order_budget_is_refused_before_anything_else()
    {
        // A market value high enough that the value terms are content at this
        // total, so what is being tested is the budget gate and nothing else.
        var facts = new CounterofferFacts(
            marketValue: 200f,
            offeredQuantity: 4,
            offeredPayment: 700f,
            enjoyment: 0.5f,
            addiction: 0.2f,
            normalisedRelationship: 0.4f,
            budgetPerOrder: 200f);

        // 3 x 200, and the gate is >=, so a dollar under it still gets an answer
        // from the rest of the method.
        Assert.Equal(0f, CounterofferOdds.Of(facts, 4, 600f));
        Assert.True(CounterofferOdds.Of(facts, 4, 599f) > 0f);
    }

    /// <summary>
    /// The value term is measured against the market value and punished by a 2.5
    /// power above it, so a steep enough per-unit price is refused outright
    /// however good everything else is.
    /// </summary>
    [Fact]
    public void A_unit_price_far_above_the_market_value_is_refused_outright()
    {
        CounterofferFacts facts = Ordinary();

        // 0.12 is the floor on the value proposition; g(x) = x^2.5 below 1, so
        // it is reached a little above twice the market value.
        Assert.Equal(0f, CounterofferOdds.Of(facts, 1, 25f * 2.4f));
    }

    /// <summary>
    /// The quantity term is a tent peaking at the quantity they asked for. This
    /// is the one rule a player can act on without any arithmetic: counter with
    /// their number.
    /// </summary>
    [Fact]
    public void Asking_for_the_quantity_they_offered_scores_best()
    {
        CounterofferFacts facts = Ordinary(quantity: 4);

        // At one price per unit, so the value term is identical in all three and
        // only the quantity tent moves. Compared at a fixed total instead, more
        // units would simply be cheaper per unit and would win on value — which
        // is a different question from the one this asks.
        const float PerUnit = 32.5f;

        float theirs = CounterofferOdds.Of(facts, 4, 4 * PerUnit);
        float fewer = CounterofferOdds.Of(facts, 2, 2 * PerUnit);
        float more = CounterofferOdds.Of(facts, 8, 8 * PerUnit);

        Assert.True(theirs >= fewer, "fewer than they asked for scored better");
        Assert.True(theirs >= more, "more than they asked for scored better");
    }

    /// <summary>
    /// And past about 3.17 times their quantity the tent has reached zero, so
    /// the whole term is dead however good the price is.
    /// </summary>
    [Fact]
    public void More_than_about_three_times_their_quantity_scores_nothing_on_the_tent()
    {
        CounterofferFacts facts = Ordinary(quantity: 4);

        Assert.Equal(0f, CounterofferOdds.Of(facts, 4 * 4, 100f));
    }

    /// <summary>
    /// Addiction enters only through <c>max(addiction, relationship)</c>, so it
    /// changes nothing at all unless it is the larger of the two. A player
    /// noticed exactly this from play and had no explanation for it.
    /// </summary>
    [Fact]
    public void Addiction_below_the_relationship_changes_nothing()
    {
        var lowAddiction = new CounterofferFacts(25f, 4, 100f, 0.5f, 0.1f, 0.4f, 200f);
        var higherAddiction = new CounterofferFacts(25f, 4, 100f, 0.5f, 0.35f, 0.4f, 200f);
        var aboveIt = new CounterofferFacts(25f, 4, 100f, 0.5f, 0.9f, 0.4f, 200f);

        float a = CounterofferOdds.Of(lowAddiction, 4, 150f);
        float b = CounterofferOdds.Of(higherAddiction, 4, 150f);
        float c = CounterofferOdds.Of(aboveIt, 4, 150f);

        Assert.Equal(a, b, 5);
        Assert.True(c >= a);
    }

    /// <summary>
    /// Asking for less money than they already offered is a better deal for
    /// them than the one they proposed, and is taken for certain.
    /// </summary>
    [Fact]
    public void A_cheaper_counter_than_their_own_offer_is_certain()
    {
        CounterofferFacts facts = Ordinary(offeredPayment: 100f, quantity: 4);

        Assert.Equal(1f, CounterofferOdds.Of(facts, 4, 90f));
    }

    /// <summary>
    /// The chance falls as the price climbs, without a step: that is what makes
    /// the search able to find a boundary at all, and it is why the roll being
    /// uniform mattered.
    /// </summary>
    [Fact]
    public void The_chance_never_climbs_as_the_price_does()
    {
        CounterofferFacts facts = Ordinary();

        float last = 1f;

        // From nothing, not from a hundred: the bisection starts at the bottom
        // of its range, and a dip there is what made it give up on the whole
        // range. Starting this at $100 is why it did not catch that.
        for (float price = 0f; price <= 560f; price += 10f)
        {
            float now = CounterofferOdds.Of(facts, 4, price);

            Assert.True(now <= last + 0.0001f, $"the chance rose at ${price}");
            last = now;
        }
    }

    /// <summary>
    /// Lisa Gardener's own numbers, out of the owner's session.
    /// </summary>
    /// <remarks>
    /// She offered three units for $780 — $260 each — against a market value of
    /// $203 and a listed price of $250. Countering at the listed price is a
    /// better deal for her than the one she proposed, so she takes it for
    /// certain. The mod answered zero at every price that night, which is what
    /// sent the search home empty.
    /// </remarks>
    [Fact]
    public void The_owners_own_customer_takes_a_counter_at_his_listed_price()
    {
        var lisa = new CounterofferFacts(
            marketValue: 203f,
            offeredQuantity: 3,
            offeredPayment: 780f,
            enjoyment: 0.5f,
            addiction: 0.2f,
            normalisedRelationship: 0.4f,
            budgetPerOrder: 632.5f);

        // Three units at his listed price of $250 each.
        Assert.Equal(1f, CounterofferOdds.Of(lisa, 3, 750f));

        // And the bottom of the search's own range is not a refusal.
        Assert.Equal(1f, CounterofferOdds.Of(lisa, 3, 0f));

        // Three times one order of hers, refused as it always was.
        Assert.Equal(0f, CounterofferOdds.Of(lisa, 3, 3f * 632.5f));
    }

    [Fact]
    public void Nothing_readable_is_no_chance_rather_than_a_guess()
    {
        CounterofferFacts facts = Ordinary();

        Assert.Equal(0f, CounterofferOdds.Of(facts, 0, 100f));
        Assert.Equal(0f, CounterofferOdds.Of(facts, 4, -1f));
        Assert.Equal(0f, CounterofferOdds.Of(default, 4, 100f));

        // Free is certain, and has to be, or the search reads the bottom of its
        // own range as a refusal.
        Assert.Equal(1f, CounterofferOdds.Of(facts, 4, 0f));
    }
}
