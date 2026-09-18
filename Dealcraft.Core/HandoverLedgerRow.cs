using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// One bonus exactly as the game named it and sized it.
/// </summary>
/// <remarks>
/// <c>Contract.BonusPayment</c> carries a <c>Title</c> and an <c>Amount</c>, and
/// the deal completion popup is handed the whole list. So the five bonuses reach
/// the screen already named and already split, and the ledger copies them across
/// without touching them. This is the row's most important content: a line
/// reading <c>Generosity Bonus $20</c> beside <c>Curfew Bonus $200</c> ends the
/// argument about what generosity pays, with no string parsing and no
/// arithmetic of ours anywhere in the path.
/// </remarks>
public sealed record ShownBonus(string Title, float Amount)
{
    public JsonObject ToJson() => new JsonObject()
        .Text("title", Title)
        .Number("amount", Amount);
}

/// <summary>
/// What the player was shown when the deal completed: the arguments of
/// <c>DealCompletionPopup.PlayPopup</c>, verbatim.
/// </summary>
/// <param name="BasePayment">
/// The popup's <c>basePayment</c>, which is <c>Contract.Payment</c> — the field
/// at <c>+0x140</c>, loaded straight into the call at <c>0x1806afc43</c>. The
/// money the player ends up with is this plus every <see cref="Bonuses"/> amount.
/// </param>
/// <param name="Satisfaction">The popup's own satisfaction figure.</param>
/// <param name="RelationshipDelta">The relationship change the popup draws.</param>
/// <param name="Bonuses">
/// The bonus list as handed to the UI, in the game's order. Empty is a real
/// answer — it means the handover earned none.
/// </param>
public sealed record ShownPopup(
    float BasePayment,
    float Satisfaction,
    float RelationshipDelta,
    IReadOnlyList<ShownBonus> Bonuses)
{
    /// <summary>The bonuses summed. Ours only in the sense that addition is ours.</summary>
    public float BonusTotal
    {
        get
        {
            float total = 0f;
            foreach (ShownBonus bonus in Bonuses)
            {
                total += bonus.Amount;
            }

            return total;
        }
    }

    public JsonObject ToJson()
    {
        var bonuses = new List<JsonObject>();
        foreach (ShownBonus bonus in Bonuses)
        {
            bonuses.Add(bonus.ToJson());
        }

        return new JsonObject()
            .Number("base_payment", BasePayment)
            .Number("satisfaction", Satisfaction)
            .Number("relationship_delta", RelationshipDelta)
            .List("bonuses", bonuses)
            .Number("bonus_total", BonusTotal);
    }
}

/// <summary>One line of the contract: what was asked for.</summary>
public sealed record RequestedLine(string ProductId, int Quality, int Quantity)
{
    public JsonObject ToJson() => new JsonObject()
        .Text("product_id", ProductId)
        .Number("quality", Quality)
        .Text("quality_name", QualityTier.Name(Quality))
        .Number("quantity", Quantity);
}

/// <summary>
/// One item that was handed over.
/// </summary>
/// <param name="Amount">The packaging size: units in one package, 1 unpackaged.</param>
/// <param name="Quantity">How many such packages.</param>
/// <param name="TotalUnits"><c>GetTotalAmount()</c>, which is the two multiplied.</param>
public sealed record DeliveredLine(
    string ProductId,
    int Quality,
    int Amount,
    int Quantity,
    int TotalUnits,
    string? Packaging)
{
    public JsonObject ToJson() => new JsonObject()
        .Text("product_id", ProductId)
        .Number("quality", Quality)
        .Text("quality_name", QualityTier.Name(Quality))
        .Number("amount", Amount)
        .Number("quantity", Quantity)
        .Number("total_units", TotalUnits)
        .Text("packaging", Packaging);
}

/// <summary>
/// The world as it stood, because four of the five bonuses depend on it.
/// </summary>
/// <remarks>
/// Every field is optional and <see cref="Unreadable"/> names what was missing.
/// A row that says it could not read the weather is worth having; a row that
/// quietly reports a zero is worth nothing, because zero is also a real reading.
/// </remarks>
public sealed class WorldReading
{
    public int? GameTime { get; set; }

    public int? DayIndex { get; set; }

    public int? WindowStart { get; set; }

    public int? WindowEnd { get; set; }

    /// <summary>
    /// Minutes since the delivery window opened, on the game's clock. Negative
    /// before it opens, which is a state the game allows and the row should show
    /// rather than clamp.
    /// </summary>
    public int? MinutesSinceWindowOpened { get; set; }

    /// <summary>
    /// Whether the game reports the quick-delivery range as current. Read from
    /// <c>TimeManager.IsCurrentDateWithinRange</c> where possible, so it is the
    /// game's answer to the game's own question rather than our subtraction.
    /// </summary>
    public bool? WithinQuickWindow { get; set; }

    public bool? CurfewActive { get; set; }

    /// <summary>
    /// The customer's own rain reading —
    /// <c>NPC.GetCurrentWeatherConditionsForEnitty().Rainy</c>, which is the
    /// float the game's fifth bonus tests against 0.10.
    /// </summary>
    public float? Rainy { get; set; }

    public float? RelationDelta { get; set; }

    public float? NormalizedRelationDelta { get; set; }

    public List<string> Unreadable { get; } = new();

    public JsonObject ToJson() => new JsonObject()
        .Number("game_time", GameTime)
        .Number("day_index", DayIndex)
        .Number("window_start", WindowStart)
        .Number("window_end", WindowEnd)
        .Number("minutes_since_window_opened", MinutesSinceWindowOpened)
        .Flag("within_quick_window", WithinQuickWindow)
        .Flag("curfew_active", CurfewActive)
        .Number("rainy", Rainy)
        .Number("relation_delta", RelationDelta)
        .Number("normalized_relation_delta", NormalizedRelationDelta)
        .Texts("unreadable", Unreadable);
}

/// <summary>
/// One handover, as one line of <c>UserData/Dealcraft/handover-ledger.jsonl</c>.
/// </summary>
/// <remarks>
/// <para>
/// The row is built from up to two independent observations of the same
/// handover, and it says which ones it got:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>What the player was shown</b> — <see cref="Shown"/>, the deal completion
/// popup's arguments. The named bonuses live here. Present only when the
/// handover happened on this machine, because the popup is UI and UI is local.
/// </description></item>
/// <item><description>
/// <b>What the server moved</b> — <see cref="TotalPayment"/> and
/// <see cref="Satisfaction"/>, off the server-side handover RPC. Present for
/// every handover the host sees, including one a remote player did, where no
/// popup ever plays here.
/// </description></item>
/// </list>
/// <para>
/// Both are the game's own numbers. Where both are present they are a check on
/// each other, which is the point: the shown bonus total and
/// <c>total_payment - contract_payment</c> are two readings of one quantity
/// taken at two different seams, and they should agree.
/// </para>
/// </remarks>
public sealed class HandoverLedgerRow
{
    /// <summary>Wall-clock time, so a row can be tied back to a moment in a session.</summary>
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Which observations this row is made of. Diagnostic, not data.</summary>
    public string Seam { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string ContractKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether the game was told a player did this by hand. False is Dealcraft's
    /// automation or a dealer; true is the control group, and the majority.
    /// </summary>
    public bool? HandoverByPlayer { get; set; }

    public string? Outcome { get; set; }

    /// <summary>The server-side RPC's <c>totalPayment</c>: payment and bonuses, clamped, as one figure.</summary>
    public float? TotalPayment { get; set; }

    /// <summary>The server-side RPC's <c>satisfaction</c>.</summary>
    public float? Satisfaction { get; set; }

    public float? ContractPayment { get; set; }

    /// <summary>
    /// <c>Contract.ExcessProductsMatchSumMultiplier</c>. Static on the game's
    /// side, so it is a constant of the build rather than a property of this
    /// contract; recorded anyway, because a row that carries it can be read
    /// years later without the binary.
    /// </summary>
    public float? ExcessProductsMatchSumMultiplier { get; set; }

    public List<RequestedLine> Requested { get; } = new();

    public int? RequestedTotalQuantity { get; set; }

    public List<DeliveredLine> Delivered { get; } = new();

    public int? DeliveredUnits { get; set; }

    /// <summary>
    /// Mean delivered grade minus requested grade, the way
    /// <c>EvaluateDelivery</c> computes it. Ours in the sense that we averaged
    /// it; every input is the game's.
    /// </summary>
    public float? QualityTiers { get; set; }

    public ShownPopup? Shown { get; set; }

    /// <summary>Why there is no popup reading, when there is none.</summary>
    public string? ShownUnavailable { get; set; }

    /// <summary>
    /// The observers RPC's third argument. Named <c>npcToRecommend</c> in the
    /// game, and it is a recommended NPC rather than a payment message — see
    /// <c>docs/handover-truth.md</c>. Recorded when it can be had, which is not
    /// on the host's own path; usually null.
    /// </summary>
    public string? NpcToRecommend { get; set; }

    public WorldReading World { get; set; } = new();

    public HandoverPrediction? Prediction { get; set; }

    /// <summary>
    /// The measured bonus total: what the server moved, less what the contract
    /// promised. The figure the prediction is answerable to.
    /// </summary>
    public float? MeasuredBonusTotal =>
        TotalPayment.HasValue && ContractPayment.HasValue
            ? TotalPayment.Value - ContractPayment.Value
            : null;

    public string ToJson()
    {
        var requested = new List<JsonObject>();
        foreach (RequestedLine line in Requested)
        {
            requested.Add(line.ToJson());
        }

        var delivered = new List<JsonObject>();
        foreach (DeliveredLine line in Delivered)
        {
            delivered.Add(line.ToJson());
        }

        return new JsonObject()
            .Text("recorded_at", RecordedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture))
            .Text("seam", Seam)
            .Text("customer", CustomerName)
            .Text("contract", ContractKey)
            .Flag("handover_by_player", HandoverByPlayer)
            .Text("outcome", Outcome)
            .Number("contract_payment", ContractPayment)
            .Number("total_payment", TotalPayment)
            .Number("measured_bonus_total", MeasuredBonusTotal)
            .Number("satisfaction", Satisfaction)
            .Number("excess_products_match_sum_multiplier", ExcessProductsMatchSumMultiplier)
            .List("requested", requested)
            .Number("requested_total_quantity", RequestedTotalQuantity)
            .List("delivered", delivered)
            .Number("delivered_units", DeliveredUnits)
            .Number("quality_tiers", QualityTiers)
            .Nest("shown", Shown?.ToJson())
            .Text("shown_unavailable", ShownUnavailable)
            .Text("npc_to_recommend", NpcToRecommend)
            .Nest("world", World.ToJson())
            .Nest("dealcraft_prediction", Prediction?.ToJson())
            .ToString();
    }
}
