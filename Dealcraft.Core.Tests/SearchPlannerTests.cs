using System;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class SearchPlannerTests
{
    [Fact]
    public void Keeps_a_real_search_inside_the_budget_it_was_given()
    {
        SearchPlan plan = SearchPlanner.For(
            minQuantity: 1,
            maxQuantity: 20,
            minPrice: 0f,
            maxPrice: 4000f,
            probeBudget: 200);

        int asked = 0;
        PriceCurve curve = PriceCurveSearch.Build(
            plan.Bounds,
            (quantity, total) =>
            {
                asked++;
                return total <= 90f * quantity;
            });

        Assert.True(plan.IsViable, plan.Problem);
        Assert.True(asked <= 200, $"the search asked the customer {asked} times against a budget of 200");
        Assert.True(curve.Probes <= plan.WorstCaseProbes);
    }

    [Fact]
    public void Recommends_a_price_a_dealer_would_recognise()
    {
        SearchPlan plan = SearchPlanner.For(
            minQuantity: 1,
            maxQuantity: 20,
            minPrice: 0f,
            maxPrice: 4000f,
            probeBudget: 200);

        CurveChoice choice = PriceCurveSearch.BestOffer(
            plan.Bounds,
            (quantity, total) => total <= 90.37f * quantity,
            floorPerUnit: 0f);

        Assert.True(choice.HasOffer, choice.Reason);

        // A round step, and a recommendation that sits on it: £1,340, never
        // £1,347.40, however awkward the customer's own threshold is.
        Assert.Contains(plan.Bounds.PriceResolution, new[] { 1f, 2f, 5f, 10f, 20f, 50f, 100f, 200f, 500f });
        Assert.Equal(
            0f,
            choice.TotalPrice - (MathF.Floor(choice.TotalPrice / plan.Bounds.PriceResolution)
                * plan.Bounds.PriceResolution),
            precision: 3);
    }

    [Fact]
    public void Never_searches_below_a_whole_unit_of_money()
    {
        SearchPlan plan = SearchPlanner.For(
            minQuantity: 1,
            maxQuantity: 2,
            minPrice: 0f,
            maxPrice: 3f,
            probeBudget: 10_000);

        Assert.Equal(1f, plan.Bounds.PriceResolution);
    }

    [Fact]
    public void Refuses_a_screen_whose_quantity_range_runs_backwards()
    {
        SearchPlan plan = SearchPlanner.For(
            minQuantity: 5,
            maxQuantity: 2,
            minPrice: 0f,
            maxPrice: 100f,
            probeBudget: 200);

        Assert.False(plan.IsViable);
        Assert.Contains("quantity", plan.Problem);
    }

    [Fact]
    public void Refuses_a_budget_too_small_to_ask_anything_useful()
    {
        SearchPlan plan = SearchPlanner.For(
            minQuantity: 1,
            maxQuantity: 40,
            minPrice: 0f,
            maxPrice: 1000f,
            probeBudget: 20);

        Assert.False(plan.IsViable);
        Assert.Contains("40 quantities", plan.Problem);
    }

    [Fact]
    public void Spends_a_generous_budget_on_a_finer_grid_than_a_tight_one()
    {
        SearchPlan tight = SearchPlanner.For(1, 20, 0f, 4000f, probeBudget: 100);
        SearchPlan generous = SearchPlanner.For(1, 20, 0f, 4000f, probeBudget: 400);

        Assert.True(
            generous.Bounds.PriceResolution < tight.Bounds.PriceResolution,
            $"a budget of 400 bought a step of {generous.Bounds.PriceResolution}, "
            + $"no finer than the {tight.Bounds.PriceResolution} a budget of 100 bought");
    }

    /// <summary>
    /// The whole derivation, pinned at its edges. This is the thing that decides
    /// how many times the running game gets asked, and every rung of it was
    /// reachable without any test noticing the answer change.
    ///
    /// Each expectation below is worked out from the stated rule, not read off
    /// the implementation:
    ///
    /// <code>
    /// quantities  = maxQ - minQ + 1
    /// perQuantity = budget / quantities          (integer division)
    /// steps       = 2^(perQuantity - 2), held at 2^20
    /// needed      = (maxPrice - minPrice) / steps
    /// resolution  = 1 when needed &lt;= 1, else the smallest of
    ///               {1, 2, 5} x 10^k that reaches needed
    /// </code>
    /// </summary>
    /// <param name="why">Which rung of the derivation this case stands on.</param>
    [Theory]
    // 1 quantity, 1000 questions: the exponent is 998. Held at 2^20 steps, so
    // needed is 4000/1048576 and the step is the finest there is. Unheld, C#
    // shifts by 998 & 31 = 6, which buys 64 steps, needs 62.5, and lands on 100.
    [InlineData(1, 1, 0f, 4000f, 1000, 1f, "the step count is held below the shift that would wrap")]
    // 20 quantities of 100 questions: 5 each, 2^3 = 8 steps, 4000/8 = 500 exactly.
    [InlineData(1, 20, 0f, 4000f, 100, 500f, "a tight budget buys a coarse step")]
    // The same range with 400: 20 each, 2^18 steps, needed is far under a unit.
    [InlineData(1, 20, 0f, 4000f, 400, 1f, "a generous budget reaches the finest step")]
    // 10 quantities of 60: 6 each, 2^4 = 16 steps, 1000/16 = 62.5, so 50 is not
    // enough and 100 is the first that reaches it.
    [InlineData(1, 10, 0f, 1000f, 60, 100f, "a step is only chosen if it reaches what is needed")]
    // 12 questions on one quantity: 2^10 = 1024 steps. A range of exactly 1024
    // needs exactly 1, which is the finest step and not a step below it.
    [InlineData(1, 1, 0f, 1024f, 12, 1f, "needing exactly one unit takes one unit")]
    // The same, over 2048: needed is exactly 2, and 2 is a step a dealer reads.
    [InlineData(1, 1, 0f, 2048f, 12, 2f, "a step that exactly reaches what is needed is taken")]
    // Over 6144: needed is exactly 6, past 5 and into the next decade.
    [InlineData(1, 1, 0f, 6144f, 12, 10f, "the ladder carries on into the next decade")]
    public void Derives_the_price_step_from_the_question_budget(
        int minQuantity,
        int maxQuantity,
        float minPrice,
        float maxPrice,
        int probeBudget,
        float expected,
        string why)
    {
        SearchPlan plan = SearchPlanner.For(minQuantity, maxQuantity, minPrice, maxPrice, probeBudget);

        Assert.True(plan.IsViable, plan.Problem);
        Assert.Equal(expected, plan.Bounds.PriceResolution);
        Assert.False(string.IsNullOrEmpty(why));
    }

    /// <summary>
    /// A range wider than any price the ladder runs out on. Taking it in one
    /// step is honest; pretending to search it at the ladder's top rung would
    /// spend the whole budget and still be nowhere near the end.
    /// </summary>
    [Fact]
    public void A_range_too_wide_to_be_a_price_is_taken_in_one_step()
    {
        SearchPlan plan = SearchPlanner.For(
            minQuantity: 1,
            maxQuantity: 1,
            minPrice: 0f,
            maxPrice: 1e10f,
            probeBudget: 2);

        Assert.True(plan.IsViable, plan.Problem);
        Assert.Equal(1e10f, plan.Bounds.PriceResolution);
    }

    [Fact]
    public void Takes_the_screens_own_limits_as_the_range_to_search()
    {
        SearchPlan plan = SearchPlanner.For(
            minQuantity: 3,
            maxQuantity: 12,
            minPrice: 50f,
            maxPrice: 900f,
            probeBudget: 200);

        Assert.Equal(3, plan.Bounds.MinQuantity);
        Assert.Equal(12, plan.Bounds.MaxQuantity);
        Assert.Equal(50f, plan.Bounds.MinTotalPrice);
        Assert.Equal(900f, plan.Bounds.MaxTotalPrice);
    }
}
