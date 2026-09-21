using System;

namespace Dealcraft.Core;

/// <summary>
/// Which of the mod's decisions a <see cref="DebugRow"/> is about.
/// </summary>
/// <remarks>
/// Four of these are the four things Dealcraft does. The fifth,
/// <see cref="Record"/>, is the record talking about itself — it is how a reader
/// finds out that the file stopped rather than that the mod did.
/// </remarks>
public enum DebugDecision
{
    /// <summary>One standing offer answered, or left alone.</summary>
    Negotiation,

    /// <summary>One offer put into a window, or not.</summary>
    Scheduling,

    /// <summary>One contract handed over, refused, or waited on.</summary>
    Handover,

    /// <summary>One product's listed price written, or left where it was.</summary>
    ListedPrice,

    /// <summary>The record itself: it filled up, or it forgot what it had said.</summary>
    Record,
}

/// <summary>
/// One decision, with its reason, as one line of
/// <c>UserData/Dealcraft/decisions.jsonl</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape is the ledger's.</b> One JSON object per line, built with the
/// same <see cref="JsonObject"/> and for the same reasons — key order is the
/// order a reader wants, and the culture is invariant so a machine whose decimal
/// separator is a comma does not write a file <c>jq</c> cannot parse. Nothing
/// here is a second format.
/// </para>
/// <para>
/// <b>A refusal carries the same keys as an action.</b> Every row of a kind
/// writes that kind's whole key set, with <c>null</c> where there is nothing to
/// put — so <c>jq 'select(.acted == false)'</c> returns rows that are as
/// complete as the ones where something happened. That is the half of this file
/// that matters: from inside the game a player cannot see why nothing happened,
/// and a row that simply left its keys out would look like a row the mod forgot
/// to fill in.
/// </para>
/// <para>
/// <b>The reason is the gate's own.</b> Nothing here re-words a decision.
/// <c>CounterofferDecision.Reason</c>, <c>ScheduleDecision.Reason</c>,
/// <c>HandoverDecision.Reason</c> and <c>ListedPriceDecision.Reason</c> are
/// already sentences the gates wrote about their own verdicts; they are copied
/// across, so what the file says is what the code decided rather than a second
/// account of it.
/// </para>
/// </remarks>
public sealed class DebugRow
{
    private DebugRow(DebugDecision decision, string outcome, bool acted, string reason)
    {
        Decision = decision;
        Outcome = outcome ?? string.Empty;
        Acted = acted;
        Reason = string.IsNullOrWhiteSpace(reason) ? "no reason was given" : reason;
    }

    /// <summary>Wall-clock time, so a row can be tied back to a moment in a session.</summary>
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Which of the four decisions this is, or the record itself.</summary>
    public DebugDecision Decision { get; }

    /// <summary>
    /// The gate's own verdict, spelled as the gate spells it —
    /// <c>Skip</c>, <c>Claim</c>, <c>Send</c>, <c>Schedule</c>, <c>Abstain</c>,
    /// <c>Wait</c>, <c>Refuse</c>, <c>HandOver</c>, <c>LeaveAlone</c>,
    /// <c>CannotCompute</c>, <c>AlreadyThere</c>, <c>Write</c>. Not translated:
    /// a reader with the file and the source should be able to find the branch.
    /// </summary>
    public string Outcome { get; }

    /// <summary>
    /// Whether anything left the mod. True for a counter-offer sent, a deal
    /// accepted, a handover completed and a price written; false for every other
    /// row, which is most of them and is the point of the file.
    /// </summary>
    public bool Acted { get; }

    /// <summary>Why. Always populated, for an action and for a refusal alike.</summary>
    public string Reason { get; }

    public string? CustomerName { get; private set; }

    public string? ContractKey { get; private set; }

    public string? ProductId { get; private set; }

    public int? Quantity { get; private set; }

    /// <summary>The counter-offer's total price, or a listed price being written.</summary>
    public float? Price { get; private set; }

    /// <summary>
    /// The chance the customer says yes, as the search found it. Never clamped:
    /// the game's own figure can exceed 1, and clamping would make 101% and 100%
    /// indistinguishable in the one file that exists to tell them apart.
    /// </summary>
    public float? Chance { get; private set; }

    /// <summary>What the customer was already offering, which a counter spends.</summary>
    public float? OfferedPayment { get; private set; }

    /// <summary>
    /// What the probing itself came to — what was handed to the game, what came
    /// back, and what threw. Null where no probe was made.
    /// </summary>
    /// <remarks>
    /// The chance above is the search's verdict and this is its working. They
    /// are both here because a chance of zero is not one fact: it is a customer
    /// who says no, an item list the game scored as nothing, or a call that
    /// threw before it reached the customer at all, and until this key existed
    /// the file spelled all three the same way. See <see cref="ProbeDiagnosis"/>.
    /// </remarks>
    public string? Probe { get; private set; }

    /// <summary>The window a deal went into, by the game's name for it.</summary>
    public string? Window { get; private set; }

    /// <summary>The grade handed over, on the game's quality ladder.</summary>
    public int? Grade { get; private set; }

    /// <summary>How many whole packages the handover came to.</summary>
    public int? Packages { get; private set; }

    /// <summary>Units handed over, packaging included, so it may overshoot.</summary>
    public int? Units { get; private set; }

    /// <summary>
    /// <c>Contract.Payment</c>: the money the handover is for. Deliberately not
    /// what it paid — that is the ledger's figure, read off the game's own RPC
    /// after the fact, and this row is written before the call. A row here and a
    /// row in <c>handover-ledger.jsonl</c> with the same contract are the two
    /// halves.
    /// </summary>
    public float? ContractPayment { get; private set; }

    /// <summary>What the product was listed at before a write.</summary>
    public float? OldPrice { get; private set; }

    /// <summary>
    /// One standing offer: what was countered, or why nothing was.
    /// </summary>
    /// <param name="chance">
    /// The search's own success chance, unclamped, or null where no search ran —
    /// which is every row the gate refused before the curve was built.
    /// </param>
    /// <param name="probe">
    /// <see cref="ProbeDiagnosis.Summary"/>: how that chance was arrived at.
    /// Null where nothing was asked, which is the same set of rows the chance is
    /// null for.
    /// </param>
    public static DebugRow Negotiation(
        string outcome,
        bool acted,
        string reason,
        string? customerName,
        string? contractKey,
        string? productId = null,
        int? quantity = null,
        float? price = null,
        float? chance = null,
        float? offeredPayment = null,
        string? probe = null) =>
        new(DebugDecision.Negotiation, outcome, acted, reason)
        {
            CustomerName = customerName,
            ContractKey = contractKey,
            ProductId = productId,
            Quantity = quantity,
            Price = price,
            Chance = chance,
            OfferedPayment = offeredPayment,
            Probe = probe,
        };

    /// <summary>One offer: which window it went into, or why none.</summary>
    public static DebugRow Scheduling(
        string outcome,
        bool acted,
        string reason,
        string? customerName,
        string? contractKey,
        string? window = null) =>
        new(DebugDecision.Scheduling, outcome, acted, reason)
        {
            CustomerName = customerName,
            ContractKey = contractKey,
            Window = window,
        };

    /// <summary>
    /// One contract: what was handed over, or which silence it was.
    /// </summary>
    /// <remarks>
    /// The three silences the spec names — nothing of the product in reach; not
    /// enough carried, or enough but needing more than four packages; nothing at
    /// or above the grade asked for — all arrive here as
    /// <c>HandoverAction.Refuse</c> or as a plan that delivers nothing, each
    /// carrying the sentence its own search wrote. They are not re-derived and
    /// they are not counted: the reason is copied and the reader reads it.
    /// </remarks>
    public static DebugRow Handover(
        string outcome,
        bool acted,
        string reason,
        string? customerName,
        string? contractKey,
        string? productId = null,
        int? grade = null,
        int? packages = null,
        int? units = null,
        float? contractPayment = null) =>
        new(DebugDecision.Handover, outcome, acted, reason)
        {
            CustomerName = customerName,
            ContractKey = contractKey,
            ProductId = productId,
            Grade = grade,
            Packages = packages,
            Units = units,
            ContractPayment = contractPayment,
        };

    /// <summary>
    /// One product's listed price: what it was, what it became, and why.
    /// </summary>
    /// <remarks>
    /// This is the one the deleted <c>Products</c> tab used to show. With
    /// <c>LIST IT AT</c> gone, a listed price Dealcraft wrote is visible nowhere
    /// in the game — <c>app.md</c> says so in as many words — so this row is the
    /// only record that it moved.
    /// </remarks>
    public static DebugRow ListedPrice(
        string outcome,
        bool acted,
        string reason,
        string? productId,
        float? oldPrice = null,
        float? newPrice = null) =>
        new(DebugDecision.ListedPrice, outcome, acted, reason)
        {
            ProductId = productId,
            OldPrice = oldPrice,
            Price = newPrice,
        };

    /// <summary>
    /// The record about itself. Never an action, and never deduplicated: it is
    /// written at the moment it becomes true.
    /// </summary>
    public static DebugRow AboutTheRecord(string outcome, string reason) =>
        new(DebugDecision.Record, outcome, acted: false, reason);

    /// <summary>
    /// What the file calls this kind of row. Lower case with an underscore, so
    /// the value is one <c>jq</c> string rather than a name to be cased.
    /// </summary>
    public static string NameOf(DebugDecision decision) => decision switch
    {
        DebugDecision.Negotiation => "negotiation",
        DebugDecision.Scheduling => "scheduling",
        DebugDecision.Handover => "handover",
        DebugDecision.ListedPrice => "listed_price",
        DebugDecision.Record => "record",
        _ => "unknown",
    };

    /// <summary>
    /// What a row is filed under when the record asks whether it has already
    /// said this: the contract where there is one, the customer or the product
    /// where there is not.
    /// </summary>
    /// <remarks>
    /// The contract first because that is what a decision is about and what
    /// outlives the sweep that found it. A customer with no offered contract,
    /// and a product, have nothing else to be filed under.
    /// </remarks>
    public string Subject()
    {
        if (!string.IsNullOrWhiteSpace(ContractKey))
        {
            return ContractKey!;
        }

        if (!string.IsNullOrWhiteSpace(CustomerName))
        {
            return CustomerName!;
        }

        if (!string.IsNullOrWhiteSpace(ProductId))
        {
            return ProductId!;
        }

        return "-";
    }

    /// <summary>
    /// The row without its clock, which is what "the same decision again" means.
    /// </summary>
    /// <remarks>
    /// Compared rather than the whole line, because the whole line carries
    /// <see cref="RecordedAt"/> and every row would therefore differ from the
    /// last one — which is exactly how a file grows a row every five seconds for
    /// a customer nothing is happening to.
    /// </remarks>
    public string Digest() => Body(new JsonObject()).ToString();

    /// <summary>One line of the file.</summary>
    public string ToJson() =>
        Body(new JsonObject().Text("recorded_at", RecordedAt.ToUniversalTime().ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture)))
            .ToString();

    /// <summary>
    /// Everything but the clock: the header every row carries, then the keys
    /// that belong to this kind of row and no others. Written into whatever is
    /// passed in, so the line and the digest are one description of the row
    /// rather than two that could drift.
    /// </summary>
    private JsonObject Body(JsonObject into)
    {
        JsonObject body = into
            .Text("decision", NameOf(Decision))
            .Text("outcome", Outcome)
            .Flag("acted", Acted)
            .Text("reason", Reason);

        switch (Decision)
        {
            case DebugDecision.Negotiation:
                return body
                    .Text("customer", CustomerName)
                    .Text("contract", ContractKey)
                    .Text("product", ProductId)
                    .Number("quantity", Quantity)
                    .Number("price", Price)
                    .Number("chance", Chance)
                    .Number("offered_payment", OfferedPayment)
                    .Text("probe", Probe);

            case DebugDecision.Scheduling:
                return body
                    .Text("customer", CustomerName)
                    .Text("contract", ContractKey)
                    .Text("window", Window);

            case DebugDecision.Handover:
                return body
                    .Text("customer", CustomerName)
                    .Text("contract", ContractKey)
                    .Text("product", ProductId)
                    .Number("grade", Grade)
                    .Text("grade_name", Grade.HasValue ? QualityTier.Name(Grade.Value) : null)
                    .Number("packages", Packages)
                    .Number("units", Units)
                    .Number("contract_payment", ContractPayment);

            case DebugDecision.ListedPrice:
                return body
                    .Text("product", ProductId)
                    .Number("old_price", OldPrice)
                    .Number("new_price", Price);

            default:
                return body;
        }
    }
}
