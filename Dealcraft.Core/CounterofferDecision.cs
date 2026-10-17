using System.Globalization;

namespace Dealcraft.Core;

public enum CounterofferOutcome
{
    /// <summary>
    /// Leave this offer alone. See the reason. The zero value on purpose: a
    /// decision nobody made must never read as permission to send anything.
    /// </summary>
    Skip,

    /// <summary>
    /// Nothing stands in the way of countering this offer. Claim the contract
    /// and then ask whether the counter is worth sending; the claim is what
    /// makes it exactly once.
    /// </summary>
    Claim,

    /// <summary>
    /// Send the counter-offer at <see cref="CounterofferDecision.Quantity"/>
    /// and <see cref="CounterofferDecision.TotalPrice"/>.
    /// </summary>
    Send,
}

/// <summary>
/// What to do about one standing offer, and why. The reason is always
/// populated and always goes in the log, including when the answer is to do
/// nothing — automation that stands down silently is indistinguishable from
/// automation that is broken.
/// </summary>
public readonly struct CounterofferDecision
{
    private readonly string? reason;

    private CounterofferDecision(CounterofferOutcome outcome, int quantity, float totalPrice, string reason)
    {
        Outcome = outcome;
        Quantity = quantity;
        TotalPrice = totalPrice;
        this.reason = reason;
    }

    public CounterofferOutcome Outcome { get; }

    /// <summary>Only meaningful for <see cref="CounterofferOutcome.Send"/>.</summary>
    public int Quantity { get; }

    /// <summary>Only meaningful for <see cref="CounterofferOutcome.Send"/>.</summary>
    public float TotalPrice { get; }

    public string Reason => reason ?? "nothing was decided";

    public static CounterofferDecision Skip(string reason) =>
        new(CounterofferOutcome.Skip, quantity: 0, totalPrice: 0f, reason);

    public static CounterofferDecision Claim(string reason) =>
        new(CounterofferOutcome.Claim, quantity: 0, totalPrice: 0f, reason);

    public static CounterofferDecision Send(in PlannedOffer offer, string reason) =>
        new(CounterofferOutcome.Send, offer.Quantity, offer.TotalPrice, reason);

    public override string ToString() =>
        Outcome == CounterofferOutcome.Send
            ? $"{Outcome} {Quantity} at {Money(TotalPrice)} ({Reason})"
            : $"{Outcome} ({Reason})";

    private static string Money(float amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
