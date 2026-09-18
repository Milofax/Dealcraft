using System;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class PricingRecommendationTests
{
    [Fact]
    public void The_recommended_price_is_the_one_that_takes_the_most_money_overall()
    {
        // Three customers, each with the per-unit price they tolerate and the
        // size they order:
        //   at 40, only the first buys:  40 x 5           =  200
        //   at 30, the first two buy:    30 x (5 + 10)    =  450
        //   at 20, all three buy:        20 x (5 + 10 +10)=  500
        PricingRecommendation best = PricingRecommendation.Best(new[]
        {
            Candidate("Jessi", quantity: 5, pricePerUnit: 40f),
            Candidate("Kyle", quantity: 10, pricePerUnit: 30f),
            Candidate("Austin", quantity: 10, pricePerUnit: 20f),
        });

        Assert.True(best.Available);
        Assert.Equal(20f, best.PricePerUnit, 2);
        Assert.Equal(500f, best.WeeklyTakings, 2);
        Assert.Equal(3, best.CustomersReached);
    }

    [Fact]
    public void One_customer_paying_well_beats_two_paying_badly()
    {
        // at 100, one buys: 100 x 1 = 100; at 10, both buy: 10 x 2 = 20.
        PricingRecommendation best = PricingRecommendation.Best(new[]
        {
            Candidate("Jessi", quantity: 1, pricePerUnit: 100f),
            Candidate("Kyle", quantity: 1, pricePerUnit: 10f),
        });

        Assert.Equal(100f, best.PricePerUnit, 2);
        Assert.Equal(100f, best.WeeklyTakings, 2);
        Assert.Equal(1, best.CustomersReached);
    }

    [Fact]
    public void A_product_nobody_can_order_has_no_recommendation()
    {
        PricingRecommendation best = PricingRecommendation.Best(Array.Empty<ProductCandidate>());

        Assert.False(best.Available);
        Assert.Equal(0f, best.PricePerUnit, 2);
        Assert.Equal(0, best.CustomersReached);
    }

    [Fact]
    public void A_customer_who_would_pay_nothing_cannot_set_the_price()
    {
        PricingRecommendation best = PricingRecommendation.Best(new[]
        {
            Candidate("Broke", quantity: 10, pricePerUnit: 0f),
            Candidate("Jessi", quantity: 2, pricePerUnit: 50f),
        });

        Assert.Equal(50f, best.PricePerUnit, 2);
        Assert.Equal(100f, best.WeeklyTakings, 2);
        Assert.Equal(1, best.CustomersReached);
    }

    [Fact]
    public void Where_two_prices_take_the_same_money_the_higher_one_wins()
    {
        // at 20, one buys: 20 x 10 = 200; at 10, both buy: 10 x (10 + 10) = 200.
        // The same takings from fewer units is the better deal to make.
        PricingRecommendation best = PricingRecommendation.Best(new[]
        {
            Candidate("Jessi", quantity: 10, pricePerUnit: 20f),
            Candidate("Kyle", quantity: 10, pricePerUnit: 10f),
        });

        Assert.Equal(20f, best.PricePerUnit, 2);
        Assert.Equal(200f, best.WeeklyTakings, 2);
    }

    private static ProductCandidate Candidate(string name, int quantity, float pricePerUnit) =>
        new(name, quantity, pricePerUnit * quantity, appeal: 0f);

    [Fact]
    public void The_points_it_evaluated_come_back_with_the_winner()
    {
        // The ladder on the Products tab is these, and they were already being
        // worked out inside this call and thrown away.
        PricingRecommendation best = PricingRecommendation.Best(new[]
        {
            Candidate("Jessi", quantity: 5, pricePerUnit: 40f),
            Candidate("Kyle", quantity: 10, pricePerUnit: 30f),
            Candidate("Austin", quantity: 10, pricePerUnit: 20f),
        });

        Assert.Equal(
            new[] { 20f, 30f, 40f },
            best.Points.Select(point => point.PricePerUnit));
        Assert.Equal(
            new[] { 3, 2, 1 },
            best.Points.Select(point => point.CustomersReached));
        Assert.Equal(
            new[] { 500f, 450f, 200f },
            best.Points.Select(point => point.WeeklyTakings));
    }

    [Fact]
    public void The_points_are_ordered_by_price_so_the_cliff_reads_left_to_right()
    {
        PricingRecommendation best = PricingRecommendation.Best(new[]
        {
            Candidate("Kyle", quantity: 10, pricePerUnit: 30f),
            Candidate("Jessi", quantity: 5, pricePerUnit: 40f),
            Candidate("Austin", quantity: 10, pricePerUnit: 20f),
        });

        Assert.Equal(new[] { 20f, 30f, 40f }, best.Points.Select(point => point.PricePerUnit));
    }

    [Fact]
    public void Two_customers_with_the_same_ceiling_are_one_point()
    {
        // A ladder that listed the same price twice would read as two rungs
        // where there is one.
        PricingRecommendation best = PricingRecommendation.Best(new[]
        {
            Candidate("Jessi", quantity: 5, pricePerUnit: 30f),
            Candidate("Kyle", quantity: 10, pricePerUnit: 30f),
        });

        ListedPricePoint only = Assert.Single(best.Points);
        Assert.Equal(30f, only.PricePerUnit, 2);
        Assert.Equal(2, only.CustomersReached);
        Assert.Equal(450f, only.WeeklyTakings, 2);
    }

    [Fact]
    public void A_product_nobody_can_order_has_no_points_either()
    {
        PricingRecommendation best = PricingRecommendation.Best(Array.Empty<ProductCandidate>());

        Assert.False(best.Available);
        Assert.Empty(best.Points);
    }

    [Fact]
    public void What_a_price_takes_can_be_asked_of_any_price_not_only_a_ceiling()
    {
        // The current listed price is rarely one of the ceilings, and the panel
        // has to say what it costs a week.
        var candidates = new[]
        {
            Candidate("Jessi", quantity: 5, pricePerUnit: 40f),
            Candidate("Kyle", quantity: 10, pricePerUnit: 30f),
        };

        ListedPricePoint at35 = PricingRecommendation.At(35f, candidates);

        Assert.Equal(35f, at35.PricePerUnit, 2);
        Assert.Equal(1, at35.CustomersReached);
        Assert.Equal(175f, at35.WeeklyTakings, 2);
    }

    [Fact]
    public void A_price_above_every_ceiling_reaches_nobody_and_takes_nothing()
    {
        ListedPricePoint dear = PricingRecommendation.At(999f, new[]
        {
            Candidate("Jessi", quantity: 5, pricePerUnit: 40f),
        });

        Assert.Equal(0, dear.CustomersReached);
        Assert.Equal(0f, dear.WeeklyTakings, 2);
    }
}
