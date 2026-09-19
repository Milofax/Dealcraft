namespace Dealcraft.Core;

/// <summary>
/// The knobs a host can turn. Mirrors the MelonPreferences entries; the core
/// never reads configuration itself.
/// </summary>
/// <remarks>
/// <para>
/// The price floor is deliberately not here. It is
/// <c>ProductManager.GetPrice(definition)</c> — the price the player listed that
/// product at — which is a different figure per product and is never a switch,
/// so it travels as an argument to <see cref="PriceCurvePolicy.Choose"/> rather
/// than as a setting somebody could turn off or type over.
/// </para>
/// <para>
/// One settings object, holding every entry the file holds — including the
/// automation switches, which used to have a mirror type of their own. The spec
/// requires that a setting exist in the file and in the app and in neither place
/// twice; two settings types is how that promise gets quietly broken, because a
/// knob added to one is not added to the other. <see cref="AutomationCatalog"/>
/// describes this type, the adapter fills it from MelonPreferences, and there is
/// no third copy.
/// </para>
/// <para>
/// <b>It is short now, and that is the ticket rather than an accident.</b> The
/// deal-scheduling switch went because four ticked windows already say which
/// windows are allowed; the price ceiling, the counter-gain floor and the probe
/// budget went because the page never drew them and a key the interface cannot
/// account for is a defect; and the exclusion list went because the game has a
/// per-customer opt-out of its own — assigning the customer to a dealer routes
/// the offer away before any of the mod's gates see it.
/// </para>
/// </remarks>
public sealed class AdvisorSettings
{
    /// <summary>Host only: send counter-offers automatically.</summary>
    public bool AutoCounterOffer { get; set; }

    /// <summary>
    /// Complete a handover the game reports as valid. Not host only: it is this
    /// player's own goods going to the customer in front of them.
    /// </summary>
    public bool AutoHandover { get; set; }

    /// <summary>
    /// Whether an automated handover may be completed without the player talking
    /// to the customer. Off, so a fresh install waits for the player to open the
    /// dialogue with them.
    /// </summary>
    /// <remarks>
    /// Read with <see cref="AutoHandover"/>, never instead of it: this says
    /// <em>when</em>, and the switch above says <em>whether</em>. The file can
    /// therefore hold three answers and no fourth — the page writes both keys
    /// with every press, so <c>AutoHandover = false</c> always comes with this
    /// one false as well. <see cref="HandoverPlace"/> is what the gate is given
    /// and carries the reading behind the default.
    /// </remarks>
    public bool HandoverFromAnywhere { get; set; }

    /// <summary>
    /// Whether a handover may go out in a better grade than the contract asked
    /// for. <b>On</b>, which is what <c>app.md</c> draws as the filled circle in
    /// the HANDOVER block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It defaulted off, on the reasoning that a fresh install should never spend
    /// the player's best product at a lower grade's price. The owner's session of
    /// 2026-09-18 settled it against that: his stock was Heavenly and his
    /// contracts asked for Standard and Poor, so every handover he had was
    /// refused with <i>"it stays in the bag"</i> and the automation did nothing
    /// at all. A default that makes the feature do nothing is the wrong default
    /// however well argued; the player who wants the strict rule can pick it, and
    /// the drawing has said so all along.
    /// </para>
    /// <para>
    /// The one setting in this file that is about goods rather than about
    /// whether a pass runs. It is not host-only: it shapes a handover, and a
    /// handover is this player's own product leaving this player's own pockets.
    /// <see cref="GradeReach"/> is what the search is given.
    /// </para>
    /// </remarks>
    public bool MayUseAHigherGrade { get; set; } = true;

    /// <summary>
    /// Never offer below this chance, 0..1. The floor the winning counter has to
    /// clear before it is sent, and the only number the player sets about
    /// negotiating.
    /// </summary>
    /// <remarks>
    /// <b>A floor, not a target.</b> It is not the confidence the curve is drawn
    /// at — <see cref="ConfidenceSearch"/> tries every rung and keeps whichever
    /// pays best — and it never reaches past that winner for another rung. It is
    /// applied once, in <see cref="CounterofferGate.Send"/>. The key is the one
    /// the preferences file has always had, because it is the same setting with
    /// the meaning stated; <see cref="ChanceFloor"/> holds the ends, the step and
    /// the argument.
    /// </remarks>
    public float AcceptanceProbabilityThreshold { get; set; } = 0.9f;

    /// <summary>
    /// A copy that can be varied without disturbing the original.
    /// </summary>
    /// <remarks>
    /// The settings object is a reading of the preferences file and is handed
    /// around freely, so a caller that needs one knob answered differently must
    /// not reach in and turn it. A test walks this type's properties by
    /// reflection and fails if one of them is not carried here, so adding a
    /// setting cannot quietly leave it behind.
    /// </remarks>
    public AdvisorSettings Copy() => new()
    {
        AutoCounterOffer = AutoCounterOffer,
        AutoHandover = AutoHandover,
        HandoverFromAnywhere = HandoverFromAnywhere,
        MayUseAHigherGrade = MayUseAHigherGrade,
        AcceptanceProbabilityThreshold = AcceptanceProbabilityThreshold,
    };

    /// <summary>
    /// The grade question as the packing search takes it. One place, so that the
    /// two spellings of the same answer cannot drift apart.
    /// </summary>
    public GradeReach Reach =>
        MayUseAHigherGrade ? GradeReach.MayUseAHigherGrade : GradeReach.Exactly;

    /// <summary>
    /// The when question as <see cref="HandoverGate"/> takes it, for the same
    /// reason: one place, so the key and the rule cannot come to disagree.
    /// </summary>
    public HandoverPlace Place =>
        HandoverFromAnywhere ? HandoverPlace.FromAnywhere : HandoverPlace.WhenITalkToThem;
}
