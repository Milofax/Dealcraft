namespace Dealcraft.Core;

/// <summary>
/// The two ways a deal's money is spelled, and the one conversion between them.
///
/// The game's own calls disagree about which they want, and the difference is
/// silent: <c>EvaluateCounteroffer(product, quantity, price)</c> and
/// <c>GetOfferSuccessChance(items, price)</c> both carry the quantity of their
/// own, so their price is the whole deal, while
/// <c>GetValueProposition(product, price)</c> carries no quantity at all and so
/// can only mean one unit. Handing the second the first's number inflates it by
/// the quantity and nothing complains. The conversion therefore lives here once,
/// named, rather than at each call site.
/// </summary>
public static class DealPrice
{
    /// <summary>
    /// What one unit costs, in a deal of <paramref name="quantity"/> units for
    /// <paramref name="totalPrice"/> together. A deal of no units has no
    /// per-unit price: that is zero rather than an error, because a screen the
    /// player has not filled in yet is routine.
    /// </summary>
    public static float PerUnit(float totalPrice, int quantity) =>
        quantity > 0 ? totalPrice / quantity : 0f;
}
