using System.Collections.Generic;
using static Dealcraft.Core.Tests.Floors;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The confidence the curve is drawn at is not the player's number to choose.
/// The curve at confidence <c>t</c> finds the highest price the customer clears
/// with chance at least <c>t</c>; what that offer is worth is
/// <c>chance × price</c>, and the <c>t</c> that maximises it is a property of
/// this customer's own curve — steep for one, flat for another. So the search
/// tries every rung and keeps the best.
/// </summary>
public class ConfidenceSearchTests
{
    /// <summary>
    /// The rungs are the ones the catalogue has always held, plus certainty.
    /// They move here because this is the only thing left that reads them.
    /// </summary>
    [Fact]
    public void The_rungs_are_the_confidences_worth_asking_for()
    {
        Assert.Equal(
            new[] { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 0.95f, 1f },
            ConfidenceSearch.Rungs);
    }

    /// <summary>
    /// The ladder has to reach certainty before the chance control's top end
    /// means anything: a player who sets the floor to 100% is asking for the
    /// offers the customer is certain to take, and a ladder stopping at 0.95
    /// would answer that with "never counter" instead.
    /// </summary>
    [Fact]
    public void The_ladder_reaches_the_top_of_the_chance_control()
    {
        Assert.Contains(ChanceFloor.Highest, ConfidenceSearch.Rungs);
        Assert.Equal(ChanceFloor.Lowest, ConfidenceSearch.Rungs[0]);
    }

    /// <summary>
    /// A customer whose curve is steep: asking for more loses the sale faster
    /// than it gains money, so the cautious rung pays best.
    /// </summary>
    [Fact]
    public void A_steep_customer_is_best_answered_carefully()
    {
        BestOffer best = ConfidenceSearch.Best(
            Curve(
                (0.5f, 200f, 0.5f),
                (0.9f, 120f, 0.9f)),
            NoChanceFloor);

        Assert.Equal(0.9f, best.Confidence);
        Assert.Equal(120f, best.Offer.TotalPrice);
        Assert.Equal(108f, best.Worth, 3);
    }

    /// <summary>
    /// And a flat one: the price climbs faster than the chance falls, so the
    /// bold rung pays best. No single number is right for both, which is the
    /// whole argument.
    /// </summary>
    [Fact]
    public void A_flat_customer_is_best_answered_boldly()
    {
        BestOffer best = ConfidenceSearch.Best(
            Curve(
                (0.5f, 400f, 0.5f),
                (0.9f, 120f, 0.9f)),
            NoChanceFloor);

        Assert.Equal(0.5f, best.Confidence);
        Assert.Equal(200f, best.Worth, 3);
    }

    /// <summary>
    /// The chance kept is the one read for the offer that will actually be
    /// made, not the rung it was searched at. The two differ because the screen
    /// rounds the price and the quantity.
    /// </summary>
    [Fact]
    public void The_chance_kept_is_the_one_read_for_the_offer_that_will_be_made()
    {
        BestOffer best = ConfidenceSearch.Best(Curve((0.9f, 120f, 0.83f)), NoChanceFloor);

        Assert.Equal(0.83f, best.Chance);
        Assert.Equal(99.6f, best.Worth, 3);
    }

    [Fact]
    public void A_rung_that_found_no_offer_is_passed_over()
    {
        BestOffer best = ConfidenceSearch.Best(new[]
        {
            new ConfidenceAttempt(0.95f, PlannedOffer.None("nothing clears your floor"), 0f),
            new ConfidenceAttempt(0.7f, PlannedOffer.Offer(4, 300f, "best of the curve"), 0.7f),
        }, NoChanceFloor);

        Assert.True(best.Found);
        Assert.Equal(0.7f, best.Confidence);
    }

    /// <summary>
    /// Every rung refusing is a real answer, and the reason the player gets is
    /// the one the most cautious rung gave — the rung that was most likely to
    /// find something.
    /// </summary>
    [Fact]
    public void Every_rung_refusing_says_why_in_the_words_of_the_rung_most_likely_to_have_found_one()
    {
        BestOffer best = ConfidenceSearch.Best(new[]
        {
            new ConfidenceAttempt(0.5f, PlannedOffer.None("nothing clears your floor of 40 per unit"), 0f),
            new ConfidenceAttempt(0.9f, PlannedOffer.None("no price at any quantity reached the confidence the search asked for"), 0f),
        }, NoChanceFloor);

        Assert.False(best.Found);
        Assert.Contains("clears your floor", best.Offer.Reason);
    }

    [Fact]
    public void Nothing_searched_at_all_is_not_an_offer()
    {
        Assert.False(ConfidenceSearch.Best(System.Array.Empty<ConfidenceAttempt>(), NoChanceFloor).Found);
    }

    /// <summary>
    /// A rung the customer would take with certainty still has to beat the
    /// others on what it is worth, not on how likely it is.
    /// </summary>
    [Fact]
    public void The_best_rung_is_the_one_worth_most_and_not_the_one_most_likely()
    {
        BestOffer best = ConfidenceSearch.Best(
            Curve(
                (0.5f, 300f, 0.6f),
                (0.95f, 100f, 1f)),
            NoChanceFloor);

        Assert.Equal(0.5f, best.Confidence);
        Assert.Equal(180f, best.Worth, 3);
    }

    /// <summary>
    /// The player's floor steers the search instead of vetoing its answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the owner's headline feature, and until 2026-09-19 it had never
    /// once fired. The search maximised <c>chance × price</c> knowing nothing of
    /// his floor, and expected value peaks in the middle of a probability curve,
    /// so the winner sat at about half confidence every time — his session has
    /// four of them at exactly 50%. The gate then held that winner against his
    /// floor and refused it. Lowering the floor from 90% to 70% changed nothing,
    /// because the winner was still at 50%.
    /// </para>
    /// <para>
    /// <i>"Das war ja das Hauptfeature, diese Preisverhandlung, dass du den
    /// optimalen Preis für 90% rausholst."</i> The optimal price <b>for</b> 90%:
    /// a constraint, which is what it now is.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_floor_picks_the_best_rung_that_clears_it_rather_than_refusing_the_best_rung()
    {
        // Shaped like Mrs. Ming's: the price falls away faster than the chance
        // climbs, so expected value peaks at the bold end.
        //   0.5 x 264 = 132   <- best overall
        //   0.7 x 180 = 126   <- best of those at 70% or better
        //   0.8 x 150 = 120
        //   0.9 x 130 = 117
        (float Rung, float Total, float Chance)[] curve =
        {
            (0.5f, 264f, 0.5f),
            (0.7f, 180f, 0.7f),
            (0.8f, 150f, 0.8f),
            (0.9f, 130f, 0.9f),
        };

        // With no floor, the middle of the curve wins on expected value: this is
        // the behaviour that was there before, and it is still right when the
        // player has asked for nothing.
        Assert.Equal(0.5f, ConfidenceSearch.Best(Curve(curve), NoChanceFloor).Confidence);

        // With his floor, the best rung that clears it — not nothing at all.
        BestOffer atSeventy = ConfidenceSearch.Best(Curve(curve), 0.7f);

        Assert.True(atSeventy.Found);
        Assert.Equal(0.7f, atSeventy.Confidence);
        Assert.Equal(180f, atSeventy.Offer.TotalPrice);
    }

    /// <summary>
    /// And a floor no rung reaches is still a refusal, which is the half of the
    /// old behaviour that was never wrong.
    /// </summary>
    [Fact]
    public void A_floor_above_every_rung_still_refuses()
    {
        BestOffer best = ConfidenceSearch.Best(
            Curve((0.5f, 264f, 0.5f), (0.7f, 180f, 0.7f)),
            0.95f);

        Assert.False(best.Found);
    }

    private static IReadOnlyList<ConfidenceAttempt> Curve(params (float Rung, float Total, float Chance)[] tried) =>
        tried
            .Select(one => new ConfidenceAttempt(
                one.Rung,
                PlannedOffer.Offer(4, one.Total, "best of the curve"),
                one.Chance))
            .ToArray();
}
