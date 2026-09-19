using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class DealPriceTests
{
    /// <summary>
    /// The counteroffer screen names one number for the whole deal. A game call
    /// that carries no quantity — <c>GetValueProposition</c> is the one that
    /// matters — must be handed the unit price instead, or it is told the
    /// customer is being charged five times what they are.
    /// </summary>
    [Fact]
    public void A_deal_total_becomes_the_price_of_one_unit()
    {
        Assert.Equal(28f, DealPrice.PerUnit(totalPrice: 140f, quantity: 5));
    }

    [Fact]
    public void One_unit_costs_the_whole_deal()
    {
        Assert.Equal(140f, DealPrice.PerUnit(totalPrice: 140f, quantity: 1));
    }

    /// <summary>
    /// An empty screen is routine, not an error: the player has opened the
    /// counteroffer interface and not chosen an amount yet.
    /// </summary>
    [Fact]
    public void A_deal_of_no_units_has_no_price_per_unit()
    {
        Assert.Equal(0f, DealPrice.PerUnit(totalPrice: 140f, quantity: 0));
        Assert.Equal(0f, DealPrice.PerUnit(totalPrice: 140f, quantity: -3));
    }
}
