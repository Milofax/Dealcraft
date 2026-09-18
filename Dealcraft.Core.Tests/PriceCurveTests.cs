using System;
using System.Collections.Generic;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class PriceCurveTests
{
    /// <summary>
    /// No listed price to hold to, which is what the game answers before a save
    /// is loaded. Named rather than written as a bare 0 at every call site.
    /// </summary>
    private const float NoFloor = 0f;

    private static OfferBounds Bounds(
        int minQuantity = 1,
        int maxQuantity = 10,
        float minTotalPrice = 0f,
        float maxTotalPrice = 1000f,
        float priceResolution = 1f,
        int maxProbes = OfferBounds.DefaultMaxProbes) =>
        new(minQuantity, maxQuantity, minTotalPrice, maxTotalPrice, priceResolution, maxProbes);

    [Fact]
    public void Finds_the_highest_total_a_monotone_customer_accepts()
    {
        var curve = PriceCurveSearch.Build(
            Bounds(minQuantity: 5, maxQuantity: 5),
            (quantity, total) => total <= 437f);

        PricePoint point = Assert.Single(curve.Points);
        Assert.True(point.Accepted);
        Assert.Equal(437f, point.TotalPrice);
    }

    [Fact]
    public void Spends_no_more_probes_than_bisection_needs_and_reports_how_many()
    {
        int asked = 0;

        var curve = PriceCurveSearch.Build(
            Bounds(minQuantity: 5, maxQuantity: 5, maxTotalPrice: 1024f),
            (quantity, total) =>
            {
                asked++;
                return total <= 437f;
            });

        // 1024 steps of £1: the ceiling, the floor, then ten halvings.
        PricePoint point = Assert.Single(curve.Points);
        Assert.True(point.Probes <= 12, $"took {point.Probes} probes");
        Assert.Equal(asked, point.Probes);
        Assert.Equal(asked, curve.Probes);
    }

    [Fact]
    public void Records_no_price_for_a_customer_who_refuses_everything()
    {
        var curve = PriceCurveSearch.Build(
            Bounds(minQuantity: 3, maxQuantity: 3),
            (quantity, total) => false);

        PricePoint point = Assert.Single(curve.Points);
        Assert.False(point.Accepted);
        Assert.Equal(0f, point.TotalPrice);
    }

    [Fact]
    public void Never_names_a_price_outside_the_callers_bounds()
    {
        var probed = new List<float>();

        var curve = PriceCurveSearch.Build(
            Bounds(minQuantity: 2, maxQuantity: 2, minTotalPrice: 50f, maxTotalPrice: 200f),
            (quantity, total) =>
            {
                probed.Add(total);
                return true; // would take any price at all
            });

        PricePoint point = Assert.Single(curve.Points);
        Assert.Equal(200f, point.TotalPrice);
        Assert.All(probed, price => Assert.InRange(price, 50f, 200f));
    }

    /// <summary>
    /// The screen's ceiling is not usually a whole number of price steps above
    /// its floor, so the top of the grid overshoots it. That top price is the
    /// first thing the customer is asked about, and asking about a price the
    /// screen would not hold means recommending one it would not hold either.
    /// </summary>
    [Fact]
    public void Never_asks_about_a_price_the_grid_overshoots_the_ceiling_with()
    {
        var probed = new List<float>();

        // 50 to 205 in steps of 100 is a step and a half: the grid's top rung
        // sits at 250, and 205 is what the screen allows.
        var curve = PriceCurveSearch.Build(
            Bounds(
                minQuantity: 1,
                maxQuantity: 1,
                minTotalPrice: 50f,
                maxTotalPrice: 205f,
                priceResolution: 100f),
            (quantity, total) =>
            {
                probed.Add(total);
                return true; // would take any price at all
            });

        Assert.All(probed, price => Assert.True(
            price <= 205f,
            $"the customer was asked about {price}, above the screen's limit of 205"));
        Assert.Equal(205f, Assert.Single(curve.Points).TotalPrice);
    }

    /// <summary>
    /// And where the grid's top rung lands exactly on the ceiling, that is the
    /// price — the clamp must not push it anywhere.
    /// </summary>
    [Fact]
    public void A_grid_that_ends_exactly_on_the_ceiling_offers_the_ceiling()
    {
        var curve = PriceCurveSearch.Build(
            Bounds(
                minQuantity: 1,
                maxQuantity: 1,
                minTotalPrice: 0f,
                maxTotalPrice: 300f,
                priceResolution: 100f),
            (quantity, total) => true);

        Assert.Equal(300f, Assert.Single(curve.Points).TotalPrice);
    }

    [Fact]
    public void Offers_only_the_quantities_the_caller_allows()
    {
        var curve = PriceCurveSearch.Build(
            Bounds(minQuantity: 4, maxQuantity: 7),
            (quantity, total) => total <= 10f * quantity);

        Assert.Equal(new[] { 4, 5, 6, 7 }, curve.Points.Select(p => p.Quantity));
        Assert.All(curve.Points, p => Assert.Equal(10f, p.PricePerUnit));
    }

    /// <summary>
    /// A customer who pays £(50 - quantity) a unit: the total rises with the
    /// deal size, the per-unit price falls. Quantity 1 costs £49, quantity 10
    /// costs £400 at £40 a unit.
    /// </summary>
    private static bool BulkBuyer(int quantity, float total) => total <= quantity * (50f - quantity);

    [Fact]
    public void Takes_the_most_money_the_customer_will_part_with()
    {
        var curve = PriceCurveSearch.Build(Bounds(), BulkBuyer);

        CurveChoice choice = PriceCurvePolicy.Choose(curve, NoFloor);

        Assert.True(choice.HasOffer);
        Assert.Equal(10, choice.Quantity);
        Assert.Equal(400f, choice.TotalPrice);
        Assert.Equal(40f, choice.PricePerUnit);
    }

    /// <summary>
    /// The floor is the price the player listed the product at, handed in per
    /// product rather than read off a setting — there is no switch that turns it
    /// off and no global figure that outranks it.
    /// </summary>
    [Fact]
    public void Strikes_out_every_quantity_priced_under_the_listed_price()
    {
        var curve = PriceCurveSearch.Build(Bounds(), BulkBuyer);

        CurveChoice choice = PriceCurvePolicy.Choose(curve, floorPerUnit: 45f);

        // Ten units would pay more, but only at £40 a unit.
        Assert.True(choice.HasOffer);
        Assert.Equal(5, choice.Quantity);
        Assert.Equal(225f, choice.TotalPrice);
        Assert.Equal(45f, choice.PricePerUnit);
    }

    /// <summary>
    /// Before a save is loaded the game has no listed price to give, and a floor
    /// of nothing bounds nothing rather than refusing every deal.
    /// </summary>
    [Fact]
    public void A_floor_of_nothing_strikes_out_nothing()
    {
        var curve = PriceCurveSearch.Build(Bounds(), BulkBuyer);

        CurveChoice choice = PriceCurvePolicy.Choose(curve, floorPerUnit: 0f);

        Assert.True(choice.HasOffer);
        Assert.Equal(10, choice.Quantity);
    }

    [Fact]
    public void Abstains_when_nothing_clears_your_floor_and_says_what_was_on_the_table()
    {
        var curve = PriceCurveSearch.Build(Bounds(), BulkBuyer);

        CurveChoice choice = PriceCurvePolicy.Choose(curve, floorPerUnit: 60f);

        Assert.False(choice.HasOffer);
        Assert.NotNull(choice.BestAvailable);
        PricePoint available = choice.BestAvailable!.Value;
        Assert.Equal(1, available.Quantity);
        Assert.Equal(49f, available.PricePerUnit);

        // The sentence the debug record carries when the floor is what refused.
        Assert.Contains("nothing clears your floor", choice.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ticket 20, the half where the cause <em>is</em> readable. The floor is
    /// the only thing that struck these quantities out, so the sentence may say
    /// so — and then it names the one move that would take the deal.
    /// </summary>
    [Fact]
    public void Says_which_price_would_take_it_when_the_floor_is_what_is_in_the_way()
    {
        var curve = PriceCurveSearch.Build(Bounds(), BulkBuyer);

        CurveChoice choice = PriceCurvePolicy.Choose(curve, floorPerUnit: 60f);

        Assert.Equal(
            "nothing clears your floor of 60 per unit; the highest the curve cleared was "
            + "49 per unit for 1, so lower the listed price to 49 to take it",
            choice.Reason);
    }

    [Fact]
    public void Abstains_without_a_price_when_no_price_reaches_the_confidence()
    {
        var curve = PriceCurveSearch.Build(Bounds(), (quantity, total) => false);

        CurveChoice choice = PriceCurvePolicy.Choose(curve, NoFloor);

        Assert.False(choice.HasOffer);
        Assert.Null(choice.BestAvailable);
        Assert.False(string.IsNullOrWhiteSpace(choice.Reason));
    }

    /// <summary>
    /// Ticket 20, the half where the cause is <b>not</b> readable, and the
    /// sentence has to say so rather than imply the other one.
    /// <para>
    /// The owner's case: he has already skimmed a customer's whole week, the mod
    /// goes quiet, and the message reads as though his price were the problem.
    /// It is not distinguishable from here — <c>GetOfferSuccessChance</c> weighs
    /// an offer against <c>GetAdjustedWeeklySpend</c>, a weekly capacity that
    /// reads no purchase history and does not fall as the week is spent
    /// (<c>docs/counteroffer-truth.md</c>). So no budget cause is claimed, the
    /// gap is named, and he is sent to the figure that does carry it.
    /// </para>
    /// </summary>
    [Fact]
    public void Claims_no_cause_it_did_not_read_when_nothing_cleared_at_any_price()
    {
        var curve = PriceCurveSearch.Build(Bounds(), (quantity, total) => false);

        CurveChoice choice = PriceCurvePolicy.Choose(curve, NoFloor);

        Assert.Equal(
            "no price at any quantity reached the confidence the search asked for; "
            + "what this customer has already spent this week is not part of that answer, "
            + "so check the week before lowering the price",
            choice.Reason);

        // The two sentences must not be confusable: this one never blames the
        // floor, and the floor's one never blames the week.
        Assert.DoesNotContain("floor", choice.Reason, StringComparison.OrdinalIgnoreCase);

        CurveChoice floored = PriceCurvePolicy.Choose(
            PriceCurveSearch.Build(Bounds(), BulkBuyer), floorPerUnit: 60f);

        Assert.DoesNotContain("week", floored.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Neither sentence blames the customer for a decision nobody read: the
    /// game hands back a chance, and the call that would settle accept-or-refuse
    /// folds a <c>Random.Range</c> into its answer.
    /// </summary>
    [Fact]
    public void Neither_silence_says_the_customer_refused()
    {
        CurveChoice nothing = PriceCurvePolicy.Choose(
            PriceCurveSearch.Build(Bounds(), (quantity, total) => false),
            NoFloor);

        CurveChoice floored = PriceCurvePolicy.Choose(
            PriceCurveSearch.Build(Bounds(), BulkBuyer), floorPerUnit: 60f);

        Assert.DoesNotContain("refus", nothing.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refus", floored.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Names_prices_only_on_the_step_the_caller_works_in()
    {
        var curve = PriceCurveSearch.Build(
            Bounds(minQuantity: 1, maxQuantity: 1, maxTotalPrice: 500f, priceResolution: 25f),
            (quantity, total) => total <= 260f);

        PricePoint point = Assert.Single(curve.Points);
        Assert.Equal(250f, point.TotalPrice);
    }

    [Fact]
    public void Refuses_bounds_that_describe_no_search_at_all()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OfferBounds(5, 2, 0f, 100f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OfferBounds(1, 10, 0f, 100f, priceResolution: 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OfferBounds(1, 10, 100f, 50f));
    }

    [Fact]
    public void Refuses_a_price_grid_too_fine_to_search_honestly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OfferBounds(1, 10, 0f, 1_000_000_000f, priceResolution: 0.0001f));
    }

    /// <summary>
    /// The curve holds a point per quantity, so a range wider than the search
    /// can hold has to be refused where the price grid is refused — by the
    /// bounds, with a message — rather than by List's allocator later on.
    /// </summary>
    [Fact]
    public void Refuses_a_quantity_range_too_wide_to_hold_a_curve()
    {
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(
            () => new OfferBounds(1, int.MaxValue, 0f, 100f));

        Assert.Contains("quantit", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refuses_a_budget_that_buys_no_probe_at_all()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OfferBounds(1, 10, 0f, 100f, maxProbes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OfferBounds(1, 10, 0f, 100f, maxProbes: -1));
    }

    /// <summary>
    /// Thirteen probes a quantity is bounded; thirteen times the quantity range
    /// is not. Every probe is an interop call into a live game, so the caller
    /// gets to say how many the whole curve may cost.
    /// </summary>
    [Fact]
    public void Never_asks_the_customer_more_often_than_the_budget_allows()
    {
        int asked = 0;

        PriceCurve curve = PriceCurveSearch.Build(
            Bounds(minQuantity: 1, maxQuantity: 500, maxProbes: 40),
            (quantity, total) =>
            {
                asked++;
                return BulkBuyer(quantity, total);
            });

        Assert.True(asked <= 40, $"asked {asked} times on a budget of 40");
        Assert.Equal(asked, curve.Probes);
    }

    /// <summary>
    /// A spent budget is an outcome, not a failure: the caller is told the
    /// curve is partial and can report it, and still gets the points that were
    /// genuinely probed.
    /// </summary>
    [Fact]
    public void Reports_a_spent_budget_instead_of_throwing()
    {
        PriceCurve curve = PriceCurveSearch.Build(Bounds(maxQuantity: 500, maxProbes: 40), BulkBuyer);

        Assert.True(curve.BudgetExhausted);
        Assert.NotEmpty(curve.Points);
        Assert.True(curve.Points.Count < 500, "a spent budget cannot have covered every quantity");
    }

    [Fact]
    public void A_budget_that_covers_the_whole_curve_is_not_reported_as_spent()
    {
        PriceCurve curve = PriceCurveSearch.Build(Bounds(), BulkBuyer);

        Assert.False(curve.BudgetExhausted);
        Assert.Equal(10, curve.Points.Count);
    }

    /// <summary>
    /// Cut short mid-bisection, the answer must still be a price the customer
    /// was seen to accept — lower than the true boundary, never higher. An
    /// offer is sent from this number.
    /// </summary>
    [Fact]
    public void Names_only_a_price_it_saw_accepted_when_the_budget_runs_out_mid_search()
    {
        PriceCurve curve = PriceCurveSearch.Build(
            Bounds(minQuantity: 5, maxQuantity: 5, maxTotalPrice: 1024f, maxProbes: 5),
            (quantity, total) => total <= 437f);

        PricePoint point = Assert.Single(curve.Points);
        Assert.True(curve.BudgetExhausted);
        Assert.True(point.Accepted);
        Assert.True(point.TotalPrice <= 437f, $"named {point.TotalPrice}, which was never accepted");
    }

    /// <summary>
    /// The caller that reports the decision is the one that must mention the
    /// truncation, so the flag has to survive the policy.
    /// </summary>
    [Fact]
    public void Carries_a_spent_budget_into_the_choice_and_says_so()
    {
        PriceCurve curve = PriceCurveSearch.Build(Bounds(maxQuantity: 500, maxProbes: 40), BulkBuyer);

        CurveChoice choice = PriceCurvePolicy.Choose(curve, NoFloor);

        Assert.True(choice.ProbeBudgetExhausted);
        Assert.Contains("budget", choice.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_choice_from_a_complete_curve_reports_no_truncation()
    {
        CurveChoice choice = PriceCurvePolicy.Choose(
            PriceCurveSearch.Build(Bounds(), BulkBuyer),
            NoFloor);

        Assert.False(choice.ProbeBudgetExhausted);
        Assert.DoesNotContain("budget", choice.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Carries_the_probe_count_into_the_choice()
    {
        var curve = PriceCurveSearch.Build(Bounds(), BulkBuyer);

        CurveChoice choice = PriceCurvePolicy.Choose(curve, NoFloor);

        Assert.Equal(curve.Probes, choice.Probes);
    }

    /// <summary>
    /// A customer with a superstition about tens: acceptance jumps about
    /// instead of falling once and staying down. The curve is then only
    /// best-effort, but it must still terminate and must never name a price
    /// the customer was never seen to accept.
    /// </summary>
    private static bool FickleBuyer(int quantity, float total) =>
        total <= 500f && ((int)total / 10) % 2 == 0;

    [Fact]
    public void Survives_a_customer_whose_acceptance_is_not_monotone_in_price()
    {
        var curve = PriceCurveSearch.Build(Bounds(minQuantity: 2, maxQuantity: 4, maxTotalPrice: 1024f), FickleBuyer);

        Assert.All(curve.Points, point =>
        {
            Assert.True(point.Probes <= 12, $"took {point.Probes} probes");
            if (point.Accepted)
            {
                Assert.True(
                    FickleBuyer(point.Quantity, point.TotalPrice),
                    $"named {point.TotalPrice} for {point.Quantity}, which was never accepted");
            }
        });
    }
}
