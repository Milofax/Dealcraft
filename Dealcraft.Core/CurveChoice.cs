namespace Dealcraft.Core;

/// <summary>
/// The point picked off the curve, or the reason no point was picked.
/// </summary>
public readonly struct CurveChoice
{
    private CurveChoice(
        bool hasOffer,
        int quantity,
        float totalPrice,
        int probes,
        string reason,
        PricePoint? bestAvailable,
        bool probeBudgetExhausted)
    {
        HasOffer = hasOffer;
        Quantity = quantity;
        TotalPrice = totalPrice;
        Probes = probes;
        Reason = reason;
        BestAvailable = bestAvailable;
        ProbeBudgetExhausted = probeBudgetExhausted;
    }

    /// <summary>False when nothing on the curve survived the floor.</summary>
    public bool HasOffer { get; }

    public int Quantity { get; }

    /// <summary>What to ask for the whole deal.</summary>
    public float TotalPrice { get; }

    public float PricePerUnit => DealPrice.PerUnit(TotalPrice, Quantity);

    /// <summary>How many times the customer was asked to reach this answer.</summary>
    public int Probes { get; }

    /// <summary>Why this point, or why none. Always populated; it goes in the log.</summary>
    public string Reason { get; }

    /// <summary>
    /// When there is no offer: the best price per unit the customer would in
    /// fact have accepted <em>on the curve as it was built</em>, so the player
    /// can judge whether the floor or the customer is wrong. Null when the
    /// customer refused everything.
    /// <para>
    /// It is the best of what was probed, not of what exists.
    /// <see cref="PriceCurveSearch.BestOffer"/> narrows the search to the
    /// target quantity before building, so nothing above the target is probed
    /// and nothing above it can appear here; a curve whose probe budget ran out
    /// is short at the top too. Both are deliberate: a probe is a call into the
    /// running game, and the saving is the point.
    /// </para>
    /// </summary>
    public PricePoint? BestAvailable { get; }

    /// <summary>
    /// True when the search stopped because <see cref="OfferBounds.MaxProbes"/>
    /// ran out. Everything here was still genuinely probed, but the curve
    /// behind it is partial, so a better deal may exist unseen. The caller
    /// reports this; it is not a failure.
    /// </summary>
    public bool ProbeBudgetExhausted { get; }

    public static CurveChoice Offer(
        int quantity,
        float totalPrice,
        int probes,
        string reason,
        bool probeBudgetExhausted = false) =>
        new(hasOffer: true, quantity, totalPrice, probes, reason, bestAvailable: null, probeBudgetExhausted);

    public static CurveChoice Abstain(
        string reason,
        int probes = 0,
        PricePoint? bestAvailable = null,
        bool probeBudgetExhausted = false) =>
        new(hasOffer: false, quantity: 0, totalPrice: 0f, probes, reason, bestAvailable, probeBudgetExhausted);

    public override string ToString() =>
        HasOffer
            ? $"{Quantity} at {TotalPrice:0.##} ({PricePerUnit:0.##} per unit, {Probes} probes) ({Reason})"
            : $"no offer ({Reason})";
}
