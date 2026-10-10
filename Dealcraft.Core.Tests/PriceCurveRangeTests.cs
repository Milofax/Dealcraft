using System;
using System.Globalization;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Ticket 51. The listed-price pass threw on every one of the owner's runs, and
/// these are the figures it threw on.
/// </summary>
/// <remarks>
/// The four constants below are the game's, read out of <c>0.4.6f13</c> and not
/// chosen here:
/// <list type="bullet">
/// <item><c>ProductManager.MIN_PRICE</c> and <c>ProductManager.MAX_PRICE</c> are
/// metadata literals <c>1</c> and <c>999</c>, and the same two numbers appear as
/// the floats and that
/// <c>RpcLogic___SetPrice</c> clamps a written price
/// between.</item>
/// <item><c>Customer.MaxOrderQuantityPerProduct</c> is a metadata literal
/// <c>1000</c>.</item>
/// <item><c>SearchPlanner.ProbeBudget</c> is the mod's own.</item>
/// </list>
/// </remarks>
public class PriceCurveRangeTests
{
    private const int LowestListablePrice = 1;
    private const int HighestListablePrice = 999;
    private const int LargestOrder = 1000;

    private static PriceCurveRange Range(
        int orderQuantity,
        int lowestPricePerUnit = LowestListablePrice,
        int highestPricePerUnit = HighestListablePrice,
        int largestOrder = LargestOrder,
        int probeBudget = SearchPlanner.ProbeBudget) =>
        PriceCurveRange.For(
            orderQuantity, lowestPricePerUnit, highestPricePerUnit, largestOrder, probeBudget);

    /// <summary>
    /// The smallest order size that reproduces the owner's failure exactly.
    /// </summary>
    /// <remarks>
    /// His sessions never recorded the number — that is half of what this ticket
    /// fixed — but the arithmetic fixes the range it was in.
    /// <c>OfferBounds.cs:83</c> refuses when
    /// <c>ceil((max − min) / step) &gt; int.MaxValue</c>, and the old reader
    /// passed <c>min = 0</c>, <c>step = 1</c> and <c>max = 999 × quantity</c>.
    /// So the throw needs <c>quantity &gt; 2,149,633</c>, and every quantity
    /// above that reproduces it identically.
    /// </remarks>
    private const int TheQuantityThatThrew = 2_149_634;

    /// <summary>
    /// The owner's own failure, twice over: what the replaced code did with
    /// these figures, and what this one does.
    /// </summary>
    [Fact]
    public void Refuses_an_order_larger_than_the_game_allows_instead_of_reaching_the_guard()
    {
        // What ProductValuationReader.cs:215 built, verbatim. It is the guard's
        // right answer and a useless one: it names the step size, which was
        // never the problem, and says nothing about the quantity, which was.
        ArgumentOutOfRangeException guard = Assert.Throws<ArgumentOutOfRangeException>(
            () => new OfferBounds(
                TheQuantityThatThrew,
                TheQuantityThatThrew,
                0f,
                HighestListablePrice * (float)TheQuantityThatThrew,
                1f,
                SearchPlanner.ProbeBudget));

        Assert.Equal("priceResolution", guard.ParamName);
        Assert.Contains("more steps of this size", guard.Message);

        // What happens now: refused one step earlier, with both figures in the
        // sentence, and without taking the rest of the product's reading with it.
        PriceCurveRange range = Range(orderQuantity: TheQuantityThatThrew);

        Assert.False(range.Searchable);
        Assert.Contains(TheQuantityThatThrew.ToString(CultureInfo.InvariantCulture), range.Refusal);
        Assert.Contains(LargestOrder.ToString(CultureInfo.InvariantCulture), range.Refusal);
        Assert.DoesNotContain("step", range.Refusal);
    }

    /// <summary>
    /// The guard's own arithmetic, from the other side: the widest band the
    /// game's four figures can produce is nowhere near what
    /// <c>OfferBounds.cs:83</c> refuses, so with the order size checked the
    /// throw cannot happen at all.
    /// </summary>
    [Fact]
    public void Builds_a_band_for_every_order_the_game_can_actually_make()
    {
        for (int quantity = 1; quantity <= LargestOrder; quantity++)
        {
            PriceCurveRange range = Range(quantity);

            Assert.True(range.Searchable, $"{quantity} units was refused: {range.Refusal}");
            Assert.Equal(quantity, range.Bounds.MinQuantity);
            Assert.Equal(quantity, range.Bounds.MaxQuantity);
            Assert.Equal(HighestListablePrice * (float)quantity, range.Bounds.MaxTotalPrice);
        }
    }

    /// <summary>
    /// The floor is the game's lowest listable price and not zero. Below it the
    /// search would answer with per-unit prices that
    /// <c>RpcLogic___SetPrice</c> raises back to a dollar on the way in, so the
    /// reading would name a price the player can never be shown.
    /// </summary>
    [Fact]
    public void Starts_at_the_lowest_price_the_game_will_hold_rather_than_at_nothing()
    {
        PriceCurveRange range = Range(orderQuantity: 7);

        Assert.Equal(LowestListablePrice * 7f, range.Bounds.MinTotalPrice);
    }

    /// <summary>A dollar, because that is what the game rounds a price to.</summary>
    [Fact]
    public void Searches_in_whole_dollars()
    {
        Assert.Equal(1f, Range(orderQuantity: 3).Bounds.PriceResolution);
        Assert.Equal(1f, PriceCurveRange.DollarStep);
    }

    [Fact]
    public void Spends_the_budget_it_was_given()
    {
        Assert.Equal(SearchPlanner.ProbeBudget, Range(orderQuantity: 3).Bounds.MaxProbes);
    }

    [Fact]
    public void Refuses_an_order_of_nothing_and_says_so()
    {
        PriceCurveRange range = Range(orderQuantity: -4);

        Assert.False(range.Searchable);
        Assert.Contains("-4", range.Refusal);
    }

    /// <summary>
    /// The game's own figures are checked before the quantity is. If those are
    /// not limits then nothing said about the order underneath them would mean
    /// anything either, and the sentence has to name the figure that was wrong.
    /// </summary>
    [Theory]
    [InlineData(0, HighestListablePrice, LargestOrder, "0")]
    [InlineData(500, 20, LargestOrder, "500")]
    [InlineData(LowestListablePrice, HighestListablePrice, 0, "0")]
    public void Refuses_limits_that_are_not_limits(
        int lowest, int highest, int largestOrder, string expected)
    {
        PriceCurveRange range = Range(
            orderQuantity: 5,
            lowestPricePerUnit: lowest,
            highestPricePerUnit: highest,
            largestOrder: largestOrder);

        Assert.False(range.Searchable);
        Assert.Contains(expected, range.Refusal);
    }

    /// <summary>
    /// The backstop, which the game's own figures cannot reach: a price band so
    /// wide that a dollar grid over it is not addressable. This is the condition
    /// <see cref="OfferBounds"/> refuses on, refused one step earlier so that
    /// both factors are in the sentence instead of the step size alone.
    /// </summary>
    [Fact]
    public void Refuses_a_band_with_more_dollar_steps_than_the_search_can_address()
    {
        PriceCurveRange range = Range(
            orderQuantity: 1000,
            highestPricePerUnit: 3_000_000);

        Assert.False(range.Searchable);
        Assert.Contains("3000000", range.Refusal.Replace(",", string.Empty));
    }

    /// <summary>
    /// A refused band has no bounds, and asking for them is a mistake that says
    /// what it was rather than handing back a price nobody named.
    /// </summary>
    [Fact]
    public void Has_no_bounds_when_it_refused()
    {
        PriceCurveRange range = Range(orderQuantity: 2_500_000);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => range.Bounds);

        Assert.Equal(range.Refusal, error.Message);
    }

    /// <summary>
    /// End to end on the band, with the search that actually consumes it: a
    /// customer who will pay up to $60 a unit for four units is found exactly,
    /// and the whole thing fits inside the budget.
    /// </summary>
    [Fact]
    public void Hands_the_search_a_band_it_can_bisect_within_the_budget()
    {
        PriceCurveRange range = Range(orderQuantity: 4);

        PriceCurve curve = PriceCurveSearch.Build(
            range.Bounds,
            (quantity, total) => total <= 240f);

        PricePoint point = Assert.Single(curve.Points);
        Assert.True(point.Accepted);
        Assert.Equal(240f, point.TotalPrice);
        Assert.False(curve.BudgetExhausted);
        Assert.True(point.Probes <= 14, $"took {point.Probes} probes");
    }
}
