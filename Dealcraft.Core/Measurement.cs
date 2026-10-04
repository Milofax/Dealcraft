namespace Dealcraft.Core;

/// <summary>
/// Whether a figure read off the scene is a measurement at all.
/// </summary>
/// <remarks>
/// <para>
/// A <c>RectTransform</c>'s rect is only meaningful after the engine has run a
/// layout pass over it. Before that it reads zero — and zero is not a narrow
/// row, it is no answer. The app's first build took it for an answer and
/// reported "names fitted against a row 0 wide", which is a decision made
/// against nothing and logged as a success.
/// </para>
/// <para>
/// So every width, height or fraction that comes out of the scene passes
/// through here first. Not a number and infinity are refused for the same
/// reason zero is: they are what a rect says when it has not been given a size,
/// not a size a widget could be fitted to.
/// </para>
/// </remarks>
public static class Measurement
{
    /// <summary>
    /// Whether <paramref name="value"/> is a real, positive measurement rather
    /// than the zero a rect holds before its first layout pass.
    /// </summary>
    public static bool IsReal(float value) =>
        IsFinite(value) && value > 0f;

    /// <summary>
    /// Whether <paramref name="value"/> is a number at all, zero and negative
    /// numbers included.
    /// </summary>
    /// <remarks>
    /// A height cannot be zero and mean anything, but a distance can: "the list
    /// stands nought pixels above the top of its window" is the answer wanted
    /// most, and "the list stands forty below it" is a real reading of a list
    /// pushed the other way. Sizes go through <see cref="IsReal"/>; offsets go
    /// through here.
    /// </remarks>
    public static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
