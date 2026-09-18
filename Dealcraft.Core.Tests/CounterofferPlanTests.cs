using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The one path both halves of the feature take. Every case here is a promise
/// about the Apply button and about the automation at once, because they are
/// the same call.
/// </summary>
public class CounterofferPlanTests
{
    private static readonly CounterofferLimits Screen = new(1, 20, 1f, 10_000f);

    [Fact]
    public void The_search_stays_on_the_quantity_the_customer_asked_for()
    {
        QuantityRange range = CounterofferPlan.Search(Screen, quantityOnScreen: 6);

        Assert.True(range.IsSingle);
        Assert.Equal(6, range.Lowest);
        Assert.Equal(6, range.Highest);
    }

    /// <summary>
    /// The quantity a hand-edited screen is holding goes through the screen's
    /// own clamp before it becomes a search range, so a field reading 9999 asks
    /// about the twenty the screen would actually hold.
    /// </summary>
    [Fact]
    public void A_quantity_the_screen_could_not_hold_is_searched_as_the_one_it_would()
    {
        QuantityRange range = CounterofferPlan.Search(Screen, quantityOnScreen: 9999);

        Assert.Equal(20, range.Lowest);
        Assert.Equal(20, range.Highest);
    }

    /// <summary>
    /// The range is a function of the quantity on the screen and nothing else —
    /// which is what makes recalculating after a hand-edited quantity the same
    /// question as opening the screen at that quantity.
    /// </summary>
    [Fact]
    public void Recalculating_at_a_quantity_asks_what_opening_at_it_would_ask()
    {
        QuantityRange afterEditing = CounterofferPlan.Search(Screen, quantityOnScreen: 13);
        QuantityRange onOpening = CounterofferPlan.Search(Screen, quantityOnScreen: 13);

        Assert.Equal(onOpening.Lowest, afterEditing.Lowest);
        Assert.Equal(onOpening.Highest, afterEditing.Highest);
    }

    [Fact]
    public void Limits_that_allow_nothing_are_searched_not_at_all()
    {
        var impossible = new CounterofferLimits(5, 2, 1f, 10f);

        Assert.False(impossible.Viable);
        Assert.False(CounterofferPlan.Search(impossible, 1).IsViable);
    }

    [Fact]
    public void A_recommendation_inside_the_limits_is_offered_as_it_stands()
    {
        PlannedOffer planned = CounterofferPlan.Offer(
            CurveChoice.Offer(8, 1350f, probes: 20, "best of the curve"), Screen);

        Assert.True(planned.HasOffer);
        Assert.Equal(8, planned.Quantity);
        Assert.Equal(1350f, planned.TotalPrice);
    }

    /// <summary>
    /// The ticket's rule: the screen's limits are hard. A recommendation past
    /// one of them is offered at the limit, and the reason says so rather than
    /// reporting a number nobody will see.
    /// </summary>
    [Fact]
    public void A_recommendation_past_a_limit_is_offered_at_the_limit()
    {
        PlannedOffer planned = CounterofferPlan.Offer(
            CurveChoice.Offer(40, 99_999f, probes: 20, "best of the curve"), Screen);

        Assert.True(planned.HasOffer);
        Assert.Equal(20, planned.Quantity);
        Assert.Equal(10_000f, planned.TotalPrice);
        Assert.Contains("the screen", planned.Reason);
    }

    /// <summary>
    /// The price selector keeps a whole number, so the plan does too. A
    /// recommendation of $1,350.60 is an offer of $1,351 wherever it is sent
    /// from.
    /// </summary>
    [Fact]
    public void A_fractional_total_is_planned_as_the_whole_one_the_selector_would_hold()
    {
        PlannedOffer planned = CounterofferPlan.Offer(
            CurveChoice.Offer(8, 1350.6f, probes: 20, "best of the curve"), Screen);

        Assert.Equal(1351f, planned.TotalPrice);
    }

    [Fact]
    public void Nothing_is_planned_when_the_curve_recommends_nothing()
    {
        PlannedOffer planned = CounterofferPlan.Offer(
            CurveChoice.Abstain("no price at any quantity reached the confidence the search asked for"), Screen);

        Assert.False(planned.HasOffer);
        Assert.Contains("reached the confidence", planned.Reason);
    }

    [Fact]
    public void Nothing_is_planned_when_the_screen_allows_nothing()
    {
        PlannedOffer planned = CounterofferPlan.Offer(
            CurveChoice.Offer(8, 1350f, probes: 20, "best of the curve"),
            new CounterofferLimits(1, 20, 900f, 100f));

        Assert.False(planned.HasOffer);
        Assert.Contains("no total between", planned.Reason);
    }

    /// <summary>
    /// The ticket's fourth box. Planning the offer the screen already holds
    /// asks the quantity control for no step at all, so a second press of Apply
    /// is a press that does nothing rather than a deal of twice the size.
    /// </summary>
    [Fact]
    public void Applying_the_plan_a_second_time_asks_for_no_change()
    {
        PlannedOffer planned = CounterofferPlan.Offer(
            CurveChoice.Offer(8, 1350f, probes: 20, "best of the curve"), Screen);

        var screen = new CounterofferScreenDouble(Screen, quantity: 3, price: 400f);

        screen.Apply(planned);
        Assert.Equal(8, screen.Quantity);
        Assert.Equal(1350f, screen.Price);

        screen.Apply(planned);
        Assert.Equal(8, screen.Quantity);
        Assert.Equal(1350f, screen.Price);
    }

    /// <summary>
    /// The plan does not depend on what the screen is holding, so applying it
    /// from anywhere lands in the same place. Without that, "the automation and
    /// the button produce an identical offer" would depend on where the player
    /// happened to leave the screen.
    /// </summary>
    [Fact]
    public void The_plan_lands_in_the_same_place_from_wherever_the_screen_started()
    {
        PlannedOffer planned = CounterofferPlan.Offer(
            CurveChoice.Offer(8, 1350f, probes: 20, "best of the curve"), Screen);

        var low = new CounterofferScreenDouble(Screen, quantity: 1, price: 1f);
        var high = new CounterofferScreenDouble(Screen, quantity: 20, price: 9_000f);

        low.Apply(planned);
        high.Apply(planned);

        Assert.Equal(low.Quantity, high.Quantity);
        Assert.Equal(low.Price, high.Price);
    }

    /// <summary>
    /// The automation sends the plan's own numbers; the button sends what the
    /// screen holds after the plan was applied to it. Those have to be the same
    /// pair, or one of the two paths is wrong.
    /// </summary>
    [Fact]
    public void What_the_screen_ends_up_holding_is_what_the_automation_would_send()
    {
        PlannedOffer planned = CounterofferPlan.Offer(
            CurveChoice.Offer(40, 99_999.5f, probes: 20, "best of the curve"), Screen);

        var screen = new CounterofferScreenDouble(Screen, quantity: 3, price: 400f);
        screen.Apply(planned);

        Assert.Equal(planned.Quantity, screen.Quantity);
        Assert.Equal(planned.TotalPrice, screen.Price);
    }

    /// <summary>
    /// The counteroffer screen, as its two controls behave. Not a mock of the
    /// game: it works the numbers through <see cref="ScreenLimits"/>, which is
    /// the reading of the game's own machine code, so a plan that survives this
    /// survives the screen.
    /// </summary>
    private sealed class CounterofferScreenDouble
    {
        private readonly CounterofferLimits _limits;

        public CounterofferScreenDouble(CounterofferLimits limits, int quantity, float price)
        {
            _limits = limits;
            Quantity = ScreenLimits.Quantity(quantity, limits.MinQuantity, limits.MaxQuantity);
            Price = ScreenLimits.Price(price, limits.MinPrice, limits.MaxPrice);
        }

        public int Quantity { get; private set; }

        public float Price { get; private set; }

        public void Apply(PlannedOffer planned)
        {
            if (!planned.HasOffer)
            {
                return;
            }

            // The quantity control takes a step, and the price selector takes a
            // destination. Exactly as the screen's own do.
            Quantity = ScreenLimits.Quantity(
                Quantity + planned.QuantityStepFrom(Quantity), _limits.MinQuantity, _limits.MaxQuantity);
            Price = ScreenLimits.Price(planned.TotalPrice, _limits.MinPrice, _limits.MaxPrice);
        }
    }
}
