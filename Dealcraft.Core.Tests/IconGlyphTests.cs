using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The home screen icon is drawn rather than shipped, so the shape is asserted
/// here instead of looked at. Coordinates are fractions of the icon, y upwards.
/// </summary>
public class IconGlyphTests
{
    private const float Feather = 2f / 128f;

    [Fact]
    public void The_upright_stroke_is_solid_and_its_left_side_is_flat()
    {
        Assert.Equal(1f, Coverage(0.35f, 0.25f), precision: 3);
        Assert.Equal(1f, Coverage(0.35f, 0.50f), precision: 3);
        Assert.Equal(1f, Coverage(0.35f, 0.75f), precision: 3);

        Assert.Equal(0f, Coverage(0.20f, 0.50f), precision: 3);
    }

    [Fact]
    public void Across_the_middle_the_letter_is_stroke_then_hole_then_stroke_then_nothing()
    {
        Assert.Equal(1f, Coverage(0.36f, 0.5f), precision: 3);
        Assert.Equal(0f, Coverage(0.55f, 0.5f), precision: 3);
        Assert.Equal(1f, Coverage(0.72f, 0.5f), precision: 3);
        Assert.Equal(0f, Coverage(0.90f, 0.5f), precision: 3);
    }

    [Fact]
    public void The_letter_keeps_a_margin_at_the_top_and_the_bottom()
    {
        Assert.Equal(0f, Coverage(0.35f, 0.95f), precision: 3);
        Assert.Equal(0f, Coverage(0.35f, 0.05f), precision: 3);
    }

    [Fact]
    public void The_bowl_closes_onto_the_stroke_at_top_and_bottom()
    {
        // Directly above the stem, at the bowl's outer edge, there is still ink;
        // an open D would have a gap here.
        Assert.Equal(1f, Coverage(0.40f, 0.80f), precision: 3);
        Assert.Equal(1f, Coverage(0.40f, 0.20f), precision: 3);
    }

    [Fact]
    public void Edges_are_partly_covered_rather_than_stepped()
    {
        float onTheEdge = Coverage(IconGlyph.LeftEdge, 0.5f);

        Assert.InRange(onTheEdge, 0.2f, 0.8f);
    }

    private static float Coverage(float u, float v) => IconGlyph.Coverage(u, v, Feather);
}
