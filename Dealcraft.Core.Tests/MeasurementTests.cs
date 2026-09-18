using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The rule the app's layout kept getting wrong: a rect that has not been laid
/// out yet reads zero, and zero is not a size.
/// </summary>
public class MeasurementTests
{
    [Fact]
    public void A_width_of_zero_is_not_a_very_narrow_row()
    {
        Assert.False(Measurement.IsReal(0f));
    }

    [Fact]
    public void A_negative_width_is_not_a_measurement_either()
    {
        Assert.False(Measurement.IsReal(-1f));
    }

    [Fact]
    public void What_a_rect_says_before_it_has_a_size_is_refused()
    {
        Assert.False(Measurement.IsReal(float.NaN));
        Assert.False(Measurement.IsReal(float.PositiveInfinity));
        Assert.False(Measurement.IsReal(float.NegativeInfinity));
    }

    [Fact]
    public void Anything_positive_is_something_to_fit_against()
    {
        Assert.True(Measurement.IsReal(0.5f));
        Assert.True(Measurement.IsReal(320f));
    }

    [Fact]
    public void A_distance_of_nothing_is_a_distance()
    {
        // "The list stands nought pixels above the top of its window" is the
        // answer being waited for, not the absence of one.
        Assert.True(Measurement.IsFinite(0f));
        Assert.True(Measurement.IsFinite(-40f));
        Assert.True(Measurement.IsFinite(467.2f));
    }

    [Fact]
    public void A_distance_that_is_not_a_number_is_still_refused()
    {
        Assert.False(Measurement.IsFinite(float.NaN));
        Assert.False(Measurement.IsFinite(float.PositiveInfinity));
        Assert.False(Measurement.IsFinite(float.NegativeInfinity));
    }
}
