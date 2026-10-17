using System;

namespace Dealcraft.Core;

/// <summary>
/// The shape of Dealcraft's home screen icon: a "D" monogram, defined as a
/// coverage function over the unit square so that it can be rasterised into a
/// texture at load time instead of shipped as a file. Coordinates are fractions
/// of the icon's width and height, y upwards.
/// </summary>
public static class IconGlyph
{
    /// <summary>Left edge of the upright stroke.</summary>
    public const float LeftEdge = 0.26f;

    /// <summary>Right edge of the upright stroke, and the bowl's centre.</summary>
    public const float StemRight = 0.38f;

    public const float BowlRadius = 0.30f;

    public const float BowlThickness = 0.12f;

    /// <summary>
    /// The stroke runs exactly as far as the bowl's outer edge, so the two
    /// strokes close into one silhouette instead of overshooting each other.
    /// </summary>
    public const float Bottom = 0.5f - BowlRadius - (BowlThickness * 0.5f);

    public const float Top = 0.5f + BowlRadius + (BowlThickness * 0.5f);

    /// <summary>
    /// How much of the pixel at (u, v) the glyph covers, 0..1.
    /// <paramref name="feather"/> is the width of the antialiased edge in the
    /// same units, normally one or two pixels.
    /// </summary>
    public static float Coverage(float u, float v, float feather)
    {
        float distance = Math.Min(StemDistance(u, v), BowlDistance(u, v));
        float coverage = 0.5f - (distance / feather);
        return coverage < 0f ? 0f : coverage > 1f ? 1f : coverage;
    }

    /// <summary>Signed distance to the upright stroke, negative inside.</summary>
    private static float StemDistance(float u, float v)
    {
        float horizontal = Math.Max(LeftEdge - u, u - StemRight);
        float vertical = Math.Max(Bottom - v, v - Top);
        return Math.Max(horizontal, vertical);
    }

    /// <summary>
    /// Signed distance to the bowl: a ring centred on the stroke's right edge,
    /// clipped to that edge so only the right half of it exists.
    /// </summary>
    private static float BowlDistance(float u, float v)
    {
        float dx = u - StemRight;
        float dy = v - 0.5f;
        float radius = (float)Math.Sqrt((dx * dx) + (dy * dy));
        float ring = Math.Abs(radius - BowlRadius) - (BowlThickness * 0.5f);
        return Math.Max(ring, StemRight - u);
    }
}
