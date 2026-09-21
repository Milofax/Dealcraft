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

    /// <summary>
    /// Two customers kept beat one, whatever the dearer rung looks like it
    /// takes.
    /// </summary>
    /// <remarks>
    /// This asserted the opposite — one customer at $100 beating two at $10,
    /// because $100 x 1 looked like more money than $10 x 2. It is not money.
    /// What a customer pays is <c>scalar x price x quantity</c> and the quantity
    /// the game gives them is <c>scalar x budget / price</c>, so the price
    /// cancels: both customers pay their own budget either way, and pricing
    /// Kyle out of the conversation throws his away for nothing.
    /// </remarks>
    [Fact]
    public void Two_customers_kept_beat_one_priced_out()
    {
        PricingRecommendation best = PricingRecommendation.Best(new[]
        {
            Candidate("Jessi", quantity: 1, pricePerUnit: 100f),
            Candidate("Kyle", quantity: 1, pricePerUnit: 10f),
        });

        Assert.Equal(10f, best.PricePerUnit, 2);
        Assert.Equal(2, best.CustomersReached);
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
    public void Where_two_prices_keep_the_same_customers_the_higher_one_wins()
    {
        // Both cliffs are the same, so no price between them loses anybody and
        // the higher one costs less stock for the same money.
        PricingRecommendation best = PricingRecommendation.Best(new[]
        {
            Candidate("Jessi", quantity: 10, pricePerUnit: 20f),
            Candidate("Kyle", quantity: 10, pricePerUnit: 20f),
        });

        Assert.Equal(20f, best.PricePerUnit, 2);
        Assert.Equal(2, best.CustomersReached);
    }

    /// <summary>
    /// One customer who barely likes the product does not set the price for
    /// everybody.
    /// </summary>
    /// <remarks>
    /// The owner asked for this before it happened: <i>"sonst optimiert es sich
    /// immer weiter, bis dann doch ein Kunde ein Ding für 5 Dollar haben möchte.
    /// Das geht natürlich nicht."</i> Keeping the most customers is the rule, and
    /// without a floor it is a ratchet — every new customer with a low cliff
    /// lowers the price for all of them, for ever, because the rule can never
    /// choose to lose one.
    /// </remarks>
    [Fact]
    public void A_customer_who_would_only_pay_five_does_not_set_the_price()
    {
        PricingRecommendation best = PricingRecommendation.Best(
            new[]
            {
                Candidate("Jessi", quantity: 1, pricePerUnit: 5f),
                Candidate("Kyle", quantity: 1, pricePerUnit: 60f),
                Candidate("Austin", quantity: 1, pricePerUnit: 80f),
            },
            marketValue: 40f);

        // Not five, and not forty either: raising Jessi's cliff to the floor
        // takes her out of the reckoning rather than putting her in it, so the
        // price is the one that suits the customers who are actually worth
        // selling to. Sixty keeps the same two as forty would and spends less
        // stock doing it.
        Assert.True(best.PricePerUnit >= 40f, $"the price fell to {best.PricePerUnit}");
        Assert.Equal(60f, best.PricePerUnit, 2);
        Assert.Equal(2, best.CustomersReached);
    }

    /// <summary>
    /// And the floor only ever raises: where every customer clears the market
    /// value, the price is theirs to set, not the floor's.
    /// </summary>
    [Fact]
    public void The_floor_does_not_pull_a_good_price_down_to_it()
    {
        PricingRecommendation best = PricingRecommendation.Best(
            new[]
            {
                Candidate("Kyle", quantity: 1, pricePerUnit: 60f),
                Candidate("Austin", quantity: 1, pricePerUnit: 80f),
            },
            marketValue: 40f);

        Assert.Equal(60f, best.PricePerUnit, 2);
        Assert.Equal(2, best.CustomersReached);
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
