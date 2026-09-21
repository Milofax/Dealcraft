using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// What the project notes says a handover should have paid.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the thing under test, not a measurement.</b> Everything else in a
/// ledger row is a number the game handed us; this is the only part Dealcraft
/// works out for itself, and it exists so that a row shows our arithmetic and
/// the game's side by side. It is written into the row under a key of its own,
/// <c>dealcraft_prediction</c>, and <c>bin/read-ledger</c> flags every row where
/// the two disagree. If they disagree often, this type is wrong and the
/// reading it came from needs re-reading — that is the outcome the ledger
/// is built to make possible, not a failure of it.
/// </para>
/// <para>
/// Every constant below was read out of the game rather than
/// remembered; the addresses are in the project notes. No value here
/// is estimated, rounded or tuned.
/// </para>
/// <para>
/// <b>Bonus row 3 was read two ways and the ledger settled it on its first
/// evening.</b> One reading made it <c>Payment x 0.15 x tiers</c>; a later
/// "correction" made it <c>Payment x 0.15 x 0.20 x tiers</c>, five times
/// smaller. This type shipped stating the second, because that is what the
/// documentation held, and said a single measured row would decide it.
/// </para>
/// <para>
/// It did, twice over. Peter File, a contract asking Poor delivered as Heavenly
/// — three tiers — on a payment of <c>495.00</c>, was paid an
/// <em>Exceeded Quality Bonus</em> of <c>222.75</c>, which is
/// <c>0.15 x 3</c> exactly. Louis Fourier, two tiers inside the quick window on
/// <c>595.00</c>, was paid <c>238.00</c>, which is <c>0.15 x 2 + 0.10</c>
/// exactly. **The first reading was right and the correction was wrong**, so the
/// tier scale is gone from the money and lives only in the gate.
/// </para>
/// <para>
/// Worth keeping in view: an independent reviewer re-derived the same
/// instructions and reproduced the wrong figure. Two careful static readings
/// agreed and both were wrong; two evenings' handovers decided it in two lines.
/// </para>
/// </remarks>
public sealed class HandoverPrediction
{
    /// <summary>Curfew and rain each pay a fifth of the payment. Read.</summary>
    public const float CurfewRate = 0.20f;

    /// <summary>Same constant, same address; named twice because they are two different bonuses.</summary>
    public const float RainyRate = 0.20f;

    /// <summary>Dollars per extra unit, flat. Read as four bytes.</summary>
    public const float GenerosityPerUnit = 10f;

    /// <summary>
    /// The Generosity Bonus is not built at all unless the delivery scores at
    /// least this. Read.
    /// </summary>
    public const float GenerositySatisfactionGate = 0.99f;

    /// <summary>The exceeded-quality rate. Read.</summary>
    public const float QualityRate = 0.15f;

    /// <summary>
    /// What the tier difference is scaled by <b>on the gate's side of the
    /// comparison only</b> — <c>xmm11</c>, read at
    ///. It was once believed to scale the payout as well,
    /// which would have made row 3 worth three percent of the payment a tier.
    /// Measured handovers show fifteen, so this never touches the money.
    /// </summary>
    public const float QualityTierGateScale = 0.20f;

    /// <summary>Quick delivery pays a tenth. Read.</summary>
    public const float QuickDeliveryRate = 0.10f;

    /// <summary>The rain has to be doing more than this for the bonus to exist.</summary>
    public const float RainyThreshold = 0.10f;

    private HandoverPrediction(
        float? curfew,
        float generosity,
        bool generosityGateMet,
        float? exceededQuality,
        float? quickDelivery,
        float? rainy,
        IReadOnlyList<string> unknown)
    {
        Curfew = curfew;
        Generosity = generosity;
        GenerosityGateMet = generosityGateMet;
        ExceededQuality = exceededQuality;
        QuickDelivery = quickDelivery;
        Rainy = rainy;
        Unknown = unknown;
    }

    /// <summary><c>Payment x 0.20</c> when curfew was active; null when we could not read whether it was.</summary>
    public float? Curfew { get; }

    /// <summary>
    /// <c>(delivered - requested) x 10</c>, never negative.
    /// </summary>
    /// <remarks>
    /// This is the spec's formula and the owner's question, so it is computed
    /// from the units that were actually handed over. The game compares against
    /// <c>matchedProductCount</c> instead — units whose similarity is exactly
    /// 1.0 — and that count is not observable at any seam we patch. The two
    /// agree whenever the delivery is the right product at or above the
    /// contract's grade with every requested property, and the game's figure is
    /// the smaller of the two otherwise. So a row where this over-states the
    /// measured bonus is the expected shape of a padded delivery, not
    /// necessarily a wrong constant.
    /// </remarks>
    public float Generosity { get; }

    /// <summary>
    /// Whether the measured satisfaction cleared
    /// <see cref="GenerositySatisfactionGate"/>. <see cref="Total"/> counts
    /// <see cref="Generosity"/> only when it did, but the figure itself is
    /// always written into the row so the gate can be judged rather than
    /// assumed.
    /// </summary>
    public bool GenerosityGateMet { get; }

    /// <summary><c>Payment x 0.15 x 0.20 x tiers</c>, once the delivery is a whole tier above the contract.</summary>
    public float? ExceededQuality { get; }

    /// <summary><c>Payment x 0.10</c> inside the first hour of the delivery window.</summary>
    public float? QuickDelivery { get; }

    /// <summary><c>Payment x 0.20</c> when the rain at the customer exceeds <see cref="RainyThreshold"/>.</summary>
    public float? Rainy { get; }

    /// <summary>
    /// The bonuses we could not predict because the world reading they need was
    /// not available. Named, so a row that disagrees can be dismissed as
    /// incomplete rather than counted as evidence.
    /// </summary>
    public IReadOnlyList<string> Unknown { get; }

    /// <summary>Whether every one of the five could be decided.</summary>
    public bool IsComplete => Unknown.Count == 0;

    /// <summary>
    /// The five summed, counting only what could be decided. Compare against
    /// the measured payment minus the contract's payment.
    /// </summary>
    public float Total
    {
        get
        {
            float total = 0f;
            total += Curfew ?? 0f;
            total += GenerosityGateMet ? Generosity : 0f;
            total += ExceededQuality ?? 0f;
            total += QuickDelivery ?? 0f;
            total += Rainy ?? 0f;
            return total;
        }
    }

    /// <param name="payment">The contract's own <c>Payment</c>, which four of the five are a share of.</param>
    /// <param name="requestedUnits"><c>ProductList.GetTotalQuantity()</c>.</param>
    /// <param name="deliveredUnits">Units actually handed over, packaging counted out.</param>
    /// <param name="satisfaction">The measured satisfaction, for the generosity gate.</param>
    /// <param name="qualityTiers">
    /// The mean, over the delivered items, of the delivered grade minus the
    /// requested one. Null when the delivery could not be read, which leaves the
    /// exceeded-quality bonus undecided rather than assumed to be zero.
    /// </param>
    /// <param name="curfewActive">Null when the curfew manager could not be read.</param>
    /// <param name="withinQuickWindow">Null when the delivery window could not be read.</param>
    /// <param name="rainy">
    /// The customer's own rain reading, which is the float the game's fifth
    /// bonus tests. Null when the weather could not be read.
    /// </param>
    public static HandoverPrediction Of(
        float payment,
        int requestedUnits,
        int deliveredUnits,
        float satisfaction,
        float? qualityTiers,
        bool? curfewActive,
        bool? withinQuickWindow,
        float? rainy)
    {
        var unknown = new List<string>();

        int extra = deliveredUnits - requestedUnits;
        float generosity = extra > 0 ? extra * GenerosityPerUnit : 0f;

        float? curfew;
        if (curfewActive.HasValue)
        {
            curfew = curfewActive.Value ? payment * CurfewRate : 0f;
        }
        else
        {
            curfew = null;
            unknown.Add("curfew");
        }

        float? quality;
        if (qualityTiers.HasValue)
        {
            // The scale belongs to the gate, not to the money: the gate asks
            // for a whole tier, and the payout is the plain rate per tier.
            // Measured — 495.00 x 0.15 x 3 = 222.75, and
            // 595.00 x (0.15 x 2 + 0.10) = 238.00 — against the alternative
            // reading, which would have predicted 74.55 and 95.20.
            float gate = qualityTiers.Value * QualityTierGateScale;
            quality = gate >= QualityTierGateScale
                ? payment * QualityRate * qualityTiers.Value
                : 0f;
        }
        else
        {
            quality = null;
            unknown.Add("exceeded_quality");
        }

        float? quick;
        if (withinQuickWindow.HasValue)
        {
            quick = withinQuickWindow.Value ? payment * QuickDeliveryRate : 0f;
        }
        else
        {
            quick = null;
            unknown.Add("quick_delivery");
        }

        float? rain;
        if (rainy.HasValue)
        {
            rain = rainy.Value > RainyThreshold ? payment * RainyRate : 0f;
        }
        else
        {
            rain = null;
            unknown.Add("rainy");
        }

        return new HandoverPrediction(
            curfew,
            generosity,
            generosityGateMet: satisfaction >= GenerositySatisfactionGate,
            quality,
            quick,
            rain,
            unknown);
    }

    /// <summary>
    /// The prediction as it goes into the row, under a key that says whose it
    /// is. Every figure is labelled <c>predicted_</c> for the same reason.
    /// </summary>
    public JsonObject ToJson() => new JsonObject()
        .Text("note", "Dealcraft's own arithmetic from docs/handover-truth.md. NOT a measurement.")
        .Number("predicted_curfew", Curfew)
        .Number("predicted_generosity", Generosity)
        .Flag("generosity_gate_met", GenerosityGateMet)
        .Number("predicted_exceeded_quality", ExceededQuality)
        .Number("predicted_quick_delivery", QuickDelivery)
        .Number("predicted_rainy", Rainy)
        .Number("predicted_total", Total)
        .Flag("complete", IsComplete)
        .Texts("undecided", Unknown);
}
