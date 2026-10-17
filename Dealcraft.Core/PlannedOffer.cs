using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// The offer to make: a quantity and a package total, already put through the
/// screen's own controls so that nothing downstream can change them.
/// </summary>
/// <remarks>
/// <para>
/// The one answer both halves of this feature work from. The Apply button dials
/// these two numbers into the screen; the automation sends the same two numbers
/// to the server without a screen. They are equal because they are this value,
/// not because two code paths were written to agree.
/// </para>
/// <para>
/// Both numbers are what the controls would hold, not what the curve asked
/// for: the quantity is clamped and whole, the total is clamped and rounded.
/// Putting them on the screen is therefore a no-op the second time, and sending
/// them names an offer the player could have dialled in by hand.
/// </para>
/// </remarks>
public readonly struct PlannedOffer
{
    private readonly string reason;

    private PlannedOffer(bool hasOffer, int quantity, float totalPrice, string reason)
    {
        HasOffer = hasOffer;
        Quantity = quantity;
        TotalPrice = totalPrice;
        this.reason = reason;
    }

    /// <summary>False when nothing can be offered. <see cref="Reason"/> says why.</summary>
    public bool HasOffer { get; }

    public int Quantity { get; }

    /// <summary>What to ask for the whole package.</summary>
    public float TotalPrice { get; }

    public float PricePerUnit => DealPrice.PerUnit(TotalPrice, Quantity);

    /// <summary>Why this offer, or why none. Always populated; it goes in the log.</summary>
    public string Reason => reason ?? "nothing was planned";

    /// <summary>
    /// How far the quantity control has to be moved to reach this offer from
    /// what the screen is holding now.
    /// </summary>
    /// <remarks>
    /// The screen's quantity control takes a step, not a destination — it is the
    /// plus and minus buttons — so handing it the wanted quantity outright would
    /// add it to what is already there and double the deal. This is the number
    /// it actually wants, and it is zero once the screen already holds the
    /// offer, which is why pressing Apply again changes nothing.
    /// </remarks>
    public float QuantityStepFrom(int quantityOnScreen) => Quantity - quantityOnScreen;

    public static PlannedOffer Offer(int quantity, float totalPrice, string reason) =>
        new(hasOffer: true, quantity, totalPrice, reason);

    public static PlannedOffer None(string reason) =>
        new(hasOffer: false, quantity: 0, totalPrice: 0f, reason);

    public override string ToString() =>
        HasOffer
            ? $"{Quantity} at {Money(TotalPrice)} ({Money(PricePerUnit)} per unit) ({Reason})"
            : $"no offer ({Reason})";

    private static string Money(float amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
