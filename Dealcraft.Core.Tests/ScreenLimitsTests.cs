using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The counteroffer screen's two controls, as the shipped machine code works
/// them. These cases are the record of what was read there; if the game ever
/// changes, these are what fail.
/// </summary>
public class ScreenLimitsTests
{
    [Fact]
    public void A_quantity_inside_the_range_is_taken_as_it_is()
    {
        Assert.Equal(7, ScreenLimits.Quantity(7f, 1, 20));
    }

    /// <summary>
    /// The screen truncates towards zero rather than rounding: it reads the
    /// input field with <c>float.TryParse</c> and casts.
    /// </summary>
    [Fact]
    public void A_fractional_quantity_loses_its_fraction()
    {
        Assert.Equal(7, ScreenLimits.Quantity(7.9f, 1, 20));
    }

    [Fact]
    public void A_quantity_below_the_floor_becomes_the_floor()
    {
        Assert.Equal(1, ScreenLimits.Quantity(0f, 1, 20));
        Assert.Equal(1, ScreenLimits.Quantity(-40f, 1, 20));
    }

    [Fact]
    public void A_quantity_above_the_ceiling_becomes_the_ceiling()
    {
        Assert.Equal(20, ScreenLimits.Quantity(9999f, 1, 20));
    }

    /// <summary>
    /// The screen's hard floor is one, whatever a caller passes as the smallest
    /// quantity. A screen that allowed five upwards raises the floor; one that
    /// claimed to allow zero does not lower it.
    /// </summary>
    [Fact]
    public void The_floor_is_never_below_one()
    {
        Assert.Equal(1, ScreenLimits.Quantity(0f, 0, 20));
        Assert.Equal(5, ScreenLimits.Quantity(3f, 5, 20));
    }

    [Fact]
    public void A_price_inside_the_range_is_taken_as_it_is()
    {
        Assert.Equal(1350f, ScreenLimits.Price(1350f, 1f, 10000f));
    }

    [Fact]
    public void A_price_outside_the_range_is_pulled_to_the_nearest_end()
    {
        Assert.Equal(1f, ScreenLimits.Price(-5f, 1f, 10000f));
        Assert.Equal(10000f, ScreenLimits.Price(99999f, 1f, 10000f));
    }

    /// <summary>
    /// The selector stores a whole number: it rounds what it is given and keeps
    /// the integer. A caller that asked for pennies never gets them back, so
    /// anything comparing what was asked with what the screen holds has to
    /// round the same way.
    /// </summary>
    [Fact]
    public void A_price_is_rounded_to_a_whole_unit()
    {
        Assert.Equal(1350f, ScreenLimits.Price(1350.4f, 1f, 10000f));
        Assert.Equal(1351f, ScreenLimits.Price(1350.6f, 1f, 10000f));
    }

    /// <summary>
    /// Half a unit goes to the even neighbour, because that is what the game's
    /// rounding helper does — it tests the low bit and steps towards it.
    /// </summary>
    [Fact]
    public void Half_a_unit_goes_to_the_even_neighbour()
    {
        Assert.Equal(1350f, ScreenLimits.Price(1350.5f, 1f, 10000f));
        Assert.Equal(1352f, ScreenLimits.Price(1351.5f, 1f, 10000f));
    }

    [Fact]
    public void The_range_is_applied_before_the_rounding()
    {
        // 9.6 would round to 10, but the ceiling is 9, so 9 is the answer.
        Assert.Equal(9f, ScreenLimits.Price(9.6f, 1f, 9f));
    }

    /// <summary>
    /// Applying what the controls already hold changes nothing. This is what
    /// makes pressing Apply twice safe: the second press asks for the number
    /// the first press produced.
    /// </summary>
    [Fact]
    public void Working_a_control_twice_lands_where_it_landed_once()
    {
        int once = ScreenLimits.Quantity(9999f, 1, 20);
        Assert.Equal(once, ScreenLimits.Quantity(once, 1, 20));

        float price = ScreenLimits.Price(1350.6f, 1f, 10000f);
        Assert.Equal(price, ScreenLimits.Price(price, 1f, 10000f));
    }

    /// <summary>
    /// A number that is not a number is not an offer. The clamp comes first, so
    /// it lands on the low end rather than propagating through the rounding.
    /// </summary>
    [Fact]
    public void A_price_that_is_not_a_number_falls_to_the_bottom_of_the_range()
    {
        Assert.Equal(1f, ScreenLimits.Price(float.NaN, 1f, 10000f));
    }
}
