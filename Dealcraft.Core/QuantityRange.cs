namespace Dealcraft.Core;

/// <summary>
/// The quantities one price search may look at. Narrower than the screen's own
/// range whenever the host has not asked for the quantity to be raised: then it
/// is the single quantity already on the table.
/// </summary>
/// <remarks>
/// This is also what the advisor caches its curve against, which is what makes
/// "recalculate after a hand-edited quantity" and "open the screen afresh at
/// that quantity" the same question. With the raise switched off the range
/// carries the quantity, so changing it invalidates the curve; with it on the
/// range does not, so changing the quantity costs nothing and answers the same.
/// </remarks>
public readonly struct QuantityRange
{
    public QuantityRange(int lowest, int highest)
    {
        Lowest = lowest;
        Highest = highest;
    }

    public int Lowest { get; }

    public int Highest { get; }

    /// <summary>Whether there is a quantity in here worth searching.</summary>
    public bool IsViable => Lowest >= ScreenLimits.SmallestQuantity && Highest >= Lowest;

    /// <summary>Whether the range names exactly one quantity.</summary>
    public bool IsSingle => Lowest == Highest;

    /// <summary>The range that names nothing, for limits no offer fits inside.</summary>
    public static QuantityRange None => new(0, 0);

    public override string ToString() =>
        IsViable
            ? IsSingle ? Lowest.ToString() : $"{Lowest}-{Highest}"
            : "no quantity";
}
