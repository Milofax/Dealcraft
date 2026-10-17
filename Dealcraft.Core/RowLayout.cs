namespace Dealcraft.Core;

/// <summary>
/// How tall a row of the app is.
/// <para>
/// The first build drew its rows as the Product Manager's square product tiles,
/// which are sized for an icon, and put two lines of text in one. What is left
/// of that lesson is the height: a row is one line of type plus the same again
/// in air, and never thinner than a floor.
/// </para>
/// <para>
/// It also carried <c>Shorten</c>, which dropped whole words off a name too wide
/// for its column. That was the master list's rule and the master list is gone.
/// </para>
/// <para>
/// Pure arithmetic over plain values. The renderer measures the real type with
/// the game's own font and asks this what to write.
/// </para>
/// </summary>
public static class RowLayout
{
    /// <summary>
    /// No row is thinner than this, whatever the type size reads, so a font the
    /// scene set small cannot collapse the list into a band of text.
    /// </summary>
    public const float ShortestRow = 20f;

    /// <summary>
    /// A row is one line of type plus the same again in air. A row exactly one
    /// line tall lets one name's descenders touch the next name's capitals, which
    /// is most of why the first build's list read as a single block.
    /// </summary>
    private const float LinesPerRow = 1.8f;

    /// <summary>
    /// How tall a row holding one line of <paramref name="fontSize"/> type is.
    /// A size the scene could not answer for — zero, or not a number — yields the
    /// floor rather than a row of no height.
    /// </summary>
    public static float Height(float fontSize)
    {
        if (float.IsNaN(fontSize) || fontSize <= 0f)
        {
            return ShortestRow;
        }

        float height = fontSize * LinesPerRow;
        return height < ShortestRow ? ShortestRow : height;
    }
}
