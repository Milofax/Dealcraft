using System;

namespace Dealcraft.Core;

/// <summary>
/// Everything <c>Customer.EvaluateCounteroffer</c> reads, as plain numbers.
/// </summary>
/// <remarks>
/// The listed price is deliberately absent: it does not appear in that method
/// at all. Only <c>MarketValue</c> and the prices in the two offers do.
/// </remarks>
public readonly struct CounterofferFacts
{
    public CounterofferFacts(
        float marketValue,
        int offeredQuantity,
        float offeredPayment,
        float enjoyment,
        float addiction,
        float normalisedRelationship,
        float budgetPerOrder)
    {
        MarketValue = marketValue;
        OfferedQuantity = offeredQuantity;
        OfferedPayment = offeredPayment;
        Enjoyment = enjoyment;
        Addiction = addiction;
        NormalisedRelationship = normalisedRelationship;
        BudgetPerOrder = budgetPerOrder;
    }

    /// <summary>What the game suggests the product is worth, per unit.</summary>
    public float MarketValue { get; }

    /// <summary>The quantity the customer asked for.</summary>
    public int OfferedQuantity { get; }

    /// <summary>The total they offered for it.</summary>
    public float OfferedPayment { get; }

    /// <summary><c>Customer.GetProductEnjoyment</c>, which may be negative.</summary>
    public float Enjoyment { get; }

    /// <summary><c>Customer.CurrentAddiction</c>.</summary>
    public float Addiction { get; }

    /// <summary><c>NPCRelationData.NormalizedRelationDelta</c>.</summary>
    public float NormalisedRelationship { get; }

    /// <summary>
    /// <c>GetAdjustedWeeklySpend(normalisedRelationship) / GetOrderDays(...).Count</c>.
    /// Three times this is the hard ceiling on a counter-offer's total.
    /// </summary>
    public float BudgetPerOrder { get; }
}

/// <summary>
/// The chance the game accepts a counter-offer, worked out rather than probed.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the method that actually decides.</b> The mod has always searched
/// over <c>Customer.GetOfferSuccessChance</c>, on the reasoning that
/// <c>EvaluateCounteroffer</c> folds a <c>UnityEngine.Random.Range</c> into the
/// bool it returns and so cannot be bisected. That reasoning was sound and the
/// conclusion was not checked: nobody ever established that the two agree, so
/// the player's confidence setting has been a floor on a different number from
/// the one that decides their deal.
/// </para>
/// <para>
/// The roll can be bisected after all, because it is uniform. Read out of
/// (constants = 3.0, =
/// 2.5, = 0.6, = 0.12,
/// = 0.2, = 0.9), in the order the method takes them:
/// </para>
/// <list type="number">
/// <item>total at or above <c>3 x budgetPerOrder</c> — refused before anything
/// else is computed;</item>
/// <item><c>qtyFactor x newValue &gt; curValue</c> — accepted outright;</item>
/// <item><c>newValue &lt; 0.12</c> — refused outright;</item>
/// <item><c>qtyFactor x enjoyNorm x newValue &gt; curValue x enjoyment</c> —
/// accepted outright;</item>
/// <item>otherwise <c>Random.Range(0, 0.9) + bonus &gt; deficit</c>.</item>
/// </list>
/// <para>
/// Because that last draw is uniform on <c>[0, 0.9)</c>, the chance of clearing
/// it is <c>(0.9 - deficit + bonus) / 0.9</c> held inside 0..1 — an exact
/// figure, not an estimate, and the search can be given it directly.
/// </para>
/// <para>
/// <b>Unproven until it runs.</b> This is a reading of the machine code by one
/// researcher, checked against two community observations it explains. Nobody
/// has watched a counter-offer go out under it. <c>CLAUDE.md</c>: the deciding
/// vote is the running code.
/// </para>
/// </remarks>
public static class CounterofferOdds
{
    /// <summary>The multiple of one order's budget a counter may not reach.</summary>
    public const float BudgetCeiling = 3f;

    /// <summary>Below this the value proposition is refused outright.</summary>
    public const float LeastValue = 0.12f;

    /// <summary>The width of the game's own draw.</summary>
    private const float RollWidth = 0.9f;

    /// <summary>How much of the deficit relationship or addiction can cover.</summary>
    private const float BonusWeight = 0.2f;

    /// <summary>The scale the deficit is measured on.</summary>
    private const float DeficitScale = 0.2f;

    /// <summary>
    /// The chance, 0..1, that this customer takes <paramref name="quantity"/>
    /// units for <paramref name="totalPrice"/>.
    /// </summary>
    public static float Of(in CounterofferFacts facts, int quantity, float totalPrice)
    {
        if (quantity <= 0 || totalPrice <= 0f || facts.OfferedQuantity <= 0)
        {
            return 0f;
        }

        // First, and before anything is computed: the budget gate.
        if (facts.BudgetPerOrder > 0f && totalPrice >= BudgetCeiling * facts.BudgetPerOrder)
        {
            return 0f;
        }

        float newValue = Value(facts.MarketValue, totalPrice / quantity);
        float curValue = Value(facts.MarketValue, facts.OfferedPayment / facts.OfferedQuantity);
        float qtyFactor = QuantityFactor(quantity, facts.OfferedQuantity);

        if (qtyFactor * newValue > curValue)
        {
            return 1f;
        }

        if (newValue < LeastValue)
        {
            return 0f;
        }

        float enjoyNorm = Clamp((facts.Enjoyment + 1f) * 0.5f, 0f, 1f);

        if (qtyFactor * enjoyNorm * newValue > curValue * facts.Enjoyment)
        {
            return 1f;
        }

        float deficit = Clamp(
            ((curValue * facts.Enjoyment) - (qtyFactor * enjoyNorm * newValue)) / DeficitScale,
            0f,
            1f);

        float bonus = Clamp(Math.Max(facts.Addiction, facts.NormalisedRelationship), 0f, 1f)
            * BonusWeight;

        // Random.Range(0, 0.9) is uniform, so the chance of clearing the deficit
        // is how much of that interval sits above it.
        return Clamp((RollWidth - deficit + bonus) / RollWidth, 0f, 1f);
    }

    /// <summary>
    /// How good a price per unit looks against the market value: linear when it
    /// is at or below it, and punished by a 2.5 power above it.
    /// </summary>
    private static float Value(float marketValue, float pricePerUnit)
    {
        if (pricePerUnit <= 0f || marketValue <= 0f)
        {
            return 0f;
        }

        float ratio = marketValue / pricePerUnit;

        return Clamp(ratio >= 1f ? ratio : (float)Math.Pow(ratio, 2.5), 0f, 2f);
    }

    /// <summary>
    /// A tent peaking at the quantity the customer asked for. Asking for more
    /// than about 3.17 times it scores zero however good the price is.
    /// </summary>
    private static float QuantityFactor(int quantity, int offeredQuantity)
    {
        float u = Clamp(0.5f * (float)Math.Pow((double)quantity / offeredQuantity, 0.6), 0f, 1f);

        return 1f - Math.Abs((2f * u) - 1f);
    }

    private static float Clamp(float value, float low, float high) =>
        value < low ? low : value > high ? high : value;
}
