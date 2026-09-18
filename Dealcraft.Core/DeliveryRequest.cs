namespace Dealcraft.Core;

/// <summary>
/// What one contract line asks for, and the two numbers that decide what
/// exceeding it is worth.
/// </summary>
/// <param name="ProductId">The product the contract names.</param>
/// <param name="RequestedQuality">
/// The grade on the contract's <c>ProductList.Entry</c>. This is the grade the
/// Exceeded Quality Bonus is measured against — not the customer's standards,
/// which are a different number entirely.
/// </param>
/// <param name="RequestedQuantity">Units, as the contract counts them.</param>
/// <param name="ContractPayment">
/// <c>Contract.Payment</c>. The quality bonus is a fraction of it.
/// </param>
/// <param name="CustomerStandard">
/// The customer's <c>Standards</c>, already mapped onto the quality ladder by
/// <c>StandardsMethod.GetCorrespondingQuality</c> — which is the identity map,
/// so <c>VeryLow</c> is Trash and <c>VeryHigh</c> is Heavenly. Standards decide
/// how much the customer enjoys the product, not what the delivery scores.
/// </param>
public sealed record DeliveryRequest(
    string ProductId,
    int RequestedQuality,
    int RequestedQuantity,
    float ContractPayment,
    int CustomerStandard);

/// <summary>How many units of one grade are within reach.</summary>
public readonly struct GradeStock
{
    public GradeStock(int quality, int units)
    {
        Quality = quality;
        Units = units;
    }

    public int Quality { get; }

    /// <summary>Units, not items: a packaged jar counts as the units it holds.</summary>
    public int Units { get; }

    public override string ToString() => $"{QualityTier.Name(Quality)} x{Units}";
}
