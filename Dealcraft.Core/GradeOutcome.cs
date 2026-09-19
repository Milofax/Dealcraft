namespace Dealcraft.Core;

/// <summary>
/// What handing over one grade would come to. Every figure here is the game's
/// own arithmetic, read out of <c>Customer.ProcessHandover</c> and
/// <c>Customer.EvaluateDelivery</c>.
/// </summary>
public readonly struct GradeOutcome
{
    public GradeOutcome(
        int quality,
        int unitsAvailable,
        int matchedProductCount,
        bool coversTheContract,
        float qualityDifference,
        float qualityBonus,
        bool clearsStandards)
    {
        Quality = quality;
        UnitsAvailable = unitsAvailable;
        MatchedProductCount = matchedProductCount;
        CoversTheContract = coversTheContract;
        QualityDifference = qualityDifference;
        QualityBonus = qualityBonus;
        ClearsStandards = clearsStandards;
    }

    public int Quality { get; }

    public int UnitsAvailable { get; }

    /// <summary>
    /// What <c>EvaluateDelivery</c> would report as <c>matchedProductCount</c>:
    /// the units of this grade that answer the contract, capped at what it asked
    /// for. Delivering more than the contract wants is what the game's
    /// Generosity Bonus is about, and Dealcraft never does it on its own.
    /// </summary>
    public int MatchedProductCount { get; }

    /// <summary>Whether this grade alone fills the whole order.</summary>
    public bool CoversTheContract { get; }

    /// <summary>
    /// <c>EvaluateDelivery</c>'s <c>qualityDifference</c>: the mean, over the
    /// delivered items, of the delivered grade's rank minus the requested one.
    /// A single-grade delivery makes that a whole number of tiers.
    /// </summary>
    public float QualityDifference { get; }

    /// <summary>
    /// The game only builds the bonus when the difference reaches
    /// <see cref="GradeChoice.QualityBonusThreshold"/>, so meeting the contract
    /// exactly earns nothing.
    /// </summary>
    public bool EarnsQualityBonus => QualityBonus > 0f;

    /// <summary>
    /// <c>Payment * 0.15 * qualityDifference</c>, which is the only term in the
    /// whole handover that keeps paying for a better grade.
    /// </summary>
    public float QualityBonus { get; }

    /// <summary>
    /// Whether this grade reaches the customer's <c>Standards</c>. That is a
    /// separate question from the contract's grade: standards feed the
    /// customer's enjoyment of the product, and one tier above them is worth as
    /// much as four.
    /// </summary>
    public bool ClearsStandards { get; }

    public override string ToString() =>
        $"{QualityTier.Name(Quality)} x{UnitsAvailable} (+{QualityBonus:0.##})";
}
