using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// One of the game's package sizes, and the most the Generosity Bonus can ever
/// be worth on it.
/// </summary>
public readonly struct Packaging
{
    public Packaging(string name, int unitsPerPackage)
    {
        Name = name;
        UnitsPerPackage = unitsPerPackage;
    }

    /// <summary>What the game calls it. Never "bundle", never "unit size".</summary>
    public string Name { get; }

    /// <summary>How many units one package holds.</summary>
    public int UnitsPerPackage { get; }

    /// <summary>The most the bonus can ever come to on this packaging.</summary>
    public float MostBonus => Generosity.MostBonus(UnitsPerPackage);
}

/// <summary>
/// What packaging rounding an order up is worth, and why it cannot be farmed.
/// <para>
/// The owner asked whether over-delivering can be farmed. It cannot, and two
/// readings in <c>docs/handover-truth.md</c> settle it between them.
/// </para>
/// <para>
/// <b>The bonus is flat.</b> <c>(delivered − requested) × 10</c>, in dollars,
/// read at RVA <c>0xA99C70</c>. Not a share of the payment.
/// </para>
/// <para>
/// <b>And it is capped by the package.</b> The match loop takes
/// <c>ceil(needed ÷ unitsPerPackage)</c> packages per stack and no more, so
/// packages beyond that never reach <c>matchedProductCount</c> and earn nothing.
/// The overshoot is therefore bounded per contract line at one package less one
/// unit — <c>$40</c> on a jar, <c>$190</c> on a brick, and on a baggie no
/// overshoot is possible at all.
/// </para>
/// <para>
/// So there is no choice to offer and no switch to build. The player cannot pad
/// an order even if he wants to: <c>HandoverGoods.Take</c> takes one product at
/// one grade, the contract's, and sorts smallest package first, which minimises
/// the overshoot. What the app does is show the ceilings beside the unit's own
/// listed price, and leave the subtraction to the player.
/// </para>
/// <para>
/// <b>What an extra unit gives up.</b> The listed price, because that is what
/// the unit would have sold for. It is <em>not</em> what the unit cost to make:
/// nothing in the mod reads a production cost and nothing can — <c>BasePrice</c>
/// and <c>MarketValue</c> are not costs, and the game's suggested price is
/// <c>BasePrice × effect multipliers</c> fixed at mixing time
/// (<c>docs/listed-price-truth.md</c>), which is not one either. So every
/// sentence about this says <b>gives up what it would have sold for</b>, never
/// "costs nothing" and never anything that implies the ingredients are priced.
/// </para>
/// <para>
/// <b>The multiplier is measured.</b> The ledger's twelve real handovers put the
/// Generosity Bonus at exactly the <c>$10</c> a unit the disassembly reads —
/// three extra units paid <c>$30</c> on the row that settled it
/// (<c>docs/handover-truth.md</c>). Every figure here scales with
/// <see cref="BonusPerUnit"/>, so a future patch that moves it moves one
/// constant.
/// </para>
/// </summary>
public static class Generosity
{
    /// <summary>
    /// What one extra unit earns, in dollars, flat. The constant the match loop
    /// multiplies by.
    /// </summary>
    public const float BonusPerUnit = 10f;

    /// <summary>
    /// The package sizes the game ships, smallest first, as named and sized in
    /// <c>docs/handover-truth.md</c>.
    /// </summary>
    /// <remarks>
    /// Held here rather than read from <c>PackagingDefinition</c>, because the
    /// sizes are asset data and the ceiling table is drawn for all three whether
    /// or not the player is carrying any. The vocabulary is the design's:
    /// <b>packaging</b>, never bundle and never unit size.
    /// </remarks>
    public static readonly IReadOnlyList<Packaging> Packagings = new[]
    {
        new Packaging("baggie", 1),
        new Packaging("jar", 5),
        new Packaging("brick", 20),
    };

    /// <summary>
    /// The most units an order can ever be overshot by on this packaging: one
    /// package less one unit. A size the game gave us nothing for overshoots by
    /// nothing rather than by a negative.
    /// </summary>
    public static int MostExtraUnits(int unitsPerPackage) =>
        unitsPerPackage > 1 ? unitsPerPackage - 1 : 0;

    /// <summary>The most the bonus can ever come to on this packaging.</summary>
    public static float MostBonus(int unitsPerPackage) =>
        MostExtraUnits(unitsPerPackage) * BonusPerUnit;

    /// <summary>
    /// Whether this packaging can earn the bonus at all. A baggie holds one
    /// unit, so no overshoot is possible and the answer is never.
    /// </summary>
    public static bool CanEverEarn(int unitsPerPackage) => MostExtraUnits(unitsPerPackage) > 0;

    /// <summary>
    /// What one extra unit comes to: the bonus, less what that unit would have
    /// sold for. Negative on any product listed above the bonus, which is nearly
    /// all of them.
    /// <para>
    /// The subtrahend is revenue forgone, not cost incurred. No production cost
    /// is known here or anywhere else in the mod; see the type's own remarks.
    /// </para>
    /// </summary>
    /// <param name="listedPricePerUnit">
    /// What the player lists the product at, which is
    /// <c>ProductManager.GetPrice</c> — the figure the adapter already reads
    /// into <see cref="ProductValuation.AskingPrice"/>.
    /// </param>
    public static float NetPerUnit(float listedPricePerUnit) =>
        BonusPerUnit - listedPricePerUnit;
}
