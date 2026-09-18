using System;

namespace Dealcraft.Core;

/// <summary>
/// Decides whether <em>this player</em> may complete a handover.
///
/// Every condition here is either the player's own configuration or an answer
/// the game gave. The gate adds no eligibility rule of its own: it does not work
/// out where the customer is, what time it is, or whether the deal is sound. It
/// asks and it obeys, and when the game says no it repeats the game's words.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gate asks the game's own question and no more of it.</b> When the
/// player presses <c>E</c> on a customer, <c>DialogueController.GetActiveChoices</c>
/// (RVA <c>0x6DC5E0</c>) lists every choice whose <c>ShouldShow()</c> is true and
/// filters on nothing else; <c>DialogueChoice.ShouldShow()</c> (RVA
/// <c>0x6D8200</c>) is <c>shouldShowCheck(Enabled)</c>; and
/// <c>Customer.SetUpDialogue</c> (RVA <c>0x6B7330</c>) sets the <i>Complete
/// Deal</i> choice's <c>shouldShowCheck</c> to
/// <c>Customer.IsReadyForHandover</c> and its <c>Enabled</c> to <c>true</c>,
/// which nothing afterwards writes. So <c>IsReadyForHandover(true)</c> <em>is</em>
/// the condition under which the option appears, and
/// <c>IsHandoverChoiceValid</c> — the same choice's <c>isValidCheck</c>, read by
/// <c>DialogueController.CheckChoice</c> (RVA <c>0x6D9C40</c>) — is the condition
/// under which pressing it does anything. <c>docs/handover-truth.md</c> carries
/// the reading.
/// </para>
/// <para>
/// <b>And it asks one question the choice does not: has the player just asked
/// for this.</b> Under <see cref="HandoverPlace.WhenITalkToThem"/> the trigger is
/// the player opening the dialogue with that customer —
/// <c>DialogueController.Interacted()</c> (RVA <c>0x6DCE70</c>), the method that
/// calls <c>GetActiveChoices()</c> at <c>0x1806dcf1c</c> and
/// <c>DialogueHandler.StartDialogue</c> at <c>0x1806dd005</c>. Pressing <c>E</c>
/// is the player saying <i>this one, now</i>, and the gate has no better signal.
/// It is an event, not a state: <see cref="HandoverSituation.TheyOpenedTheDialogue"/>
/// is true only on the pass that the opening caused.
/// </para>
/// <para>
/// <b>That replaces a distance, and the distance is deleted rather than left
/// unreachable.</b> Ticket 49 read the game's own proximity threshold —
/// <c>InteractionManager.CheckHover</c> (RVA <c>0x64ABB0</c>) comparing
/// <c>Vector3.Distance(playerCamera.position, hitPoint)</c> against that
/// object's own <c>MaxInteractionRange</c> (<c>+0x30</c>, at
/// <c>0x18064b21b</c>–<c>0x18064b223</c>) — and gated on it. The reading stands
/// and stays in <c>docs/handover-truth.md</c>. The gate does not, because a
/// distance was a proxy for intent and the owner has named the intent. It also
/// took the one figure in this feature that was never read out of the binary
/// with it: the mod ran no raycast, so it measured root to root.
/// </para>
/// <para>
/// <b>It used to ask two more, and the game asks neither.</b>
/// <c>Customer.IsAtDealLocation</c> (RVA <c>0x6AB4B0</c>) is called from exactly
/// two places in <c>GameAssembly.dll</c>, both inside
/// <c>DealerAttendDealBehaviour</c> — an NPC dealer delivering for you.
/// <c>Customer.IsDealTime</c> (RVA <c>0x6AB620</c>) is called from exactly two,
/// both inside <c>Customer.OnMinPass</c>, one feeding the customer's debug log
/// and one gating <c>ShouldTryGenerateDeal</c>. Neither is on the path a player
/// takes, so neither is on ours. The game's own timing answer is inside
/// <c>IsReadyForHandover</c> already: it returns <c>Customer.IsAwaitingDelivery</c>,
/// which <c>CustomerAttendDealBehaviour</c> raises when the customer sets out for
/// the deal and drops when it is over.
/// </para>
/// <para>
/// <b>This gate used to refuse unless the machine was the server, and that rule
/// was ours rather than the game's.</b> <c>Customer.ProcessHandover</c> (RVA
/// <c>0x6AF330</c>) reads <c>IsClientInitialized</c> and calls
/// <c>SendServerRpc</c> — there is no <c>IsServerInitialized</c> anywhere in it.
/// It is the path a guest's own Done button takes, and any client may take it.
/// </para>
/// <para>
/// <b>And the game's own readiness question is already per player.</b>
/// <c>Customer.IsReadyForHandover</c> reads <c>Player.Local.CrimeData</c>, so on
/// every machine it answers about the player standing there. A handover requires
/// <em>this player is here with the goods</em>, which is what these conditions
/// already say, and never <em>this machine is the server</em>.
/// </para>
/// <para>
/// Negotiating and scheduling keep the server requirement and the asymmetry is
/// the point: one shared conversation must not be answered six times, but one
/// player's goods leaving one player's pockets belong to whoever is carrying
/// them. What is still forbidden is unchanged — never write into another
/// player's state to fake an effect they did not cause.
/// </para>
/// </remarks>
public static class HandoverGate
{
    public static HandoverDecision Decide(HandoverSituation situation)
    {
        if (situation is null)
        {
            throw new ArgumentNullException(nameof(situation));
        }

        if (!situation.CanReachTheServer)
        {
            // Not a rule of ours: ProcessHandover writes through the client and
            // is dropped with a warning without one. Nothing about who hosts.
            return HandoverDecision.Abstain(
                "this machine is not connected, so the handover call has nowhere to go");
        }

        if (!situation.AutomationEnabled)
        {
            return HandoverDecision.Abstain("automatic handover is switched off");
        }

        if (string.IsNullOrWhiteSpace(situation.ContractKey))
        {
            return HandoverDecision.Abstain("the customer has no contract that can be identified");
        }

        // There is no exclusion list any more, and here the owner's reason held
        // without any mod code from the start: the game itself refuses a
        // handover for a contract a dealer owns, which is the branch below on
        // IsHandoverChoiceValid.
        // Refusing outranks waiting. A handover the game calls invalid — a
        // contract a dealer already owns, say — does not become valid by
        // waiting, and the reason is worth saying once rather than never.
        if (!situation.IsHandoverChoiceValid)
        {
            return HandoverDecision.Refuse(
                $"the game refused the handover for {Named(situation)}: {InvalidReason(situation)}");
        }

        // The game's own test for putting *Complete Deal* on the prompt.
        if (!situation.IsReadyForHandover)
        {
            return HandoverDecision.Wait($"the game does not report {Named(situation)} as ready to hand over");
        }

        // And the player's own signal, asked last because it is the one he can
        // answer by walking over and pressing E. A sweep never carries it: the
        // question is whether a dialogue just opened, not whether one is open.
        if (situation.Place == HandoverPlace.WhenITalkToThem && !situation.TheyOpenedTheDialogue)
        {
            return HandoverDecision.Wait(
                $"{Named(situation)} is ready and Dealcraft is waiting for you to talk to them");
        }

        return HandoverDecision.HandOver(
            $"the game would offer Complete Deal on {Named(situation)} and calls the handover valid, "
                + Under(situation));
    }

    /// <summary>
    /// Which of the two answers let this handover through, said in the decision
    /// itself so that a session can be read back without knowing what the
    /// preferences file held at the time.
    /// </summary>
    /// <remarks>
    /// This is the sentence <c>decisions.jsonl</c> keeps, and under
    /// <see cref="HandoverPlace.WhenITalkToThem"/> it has to name the dialogue
    /// opening rather than the setting: the setting is what the file held, the
    /// opening is what happened.
    /// </remarks>
    private static string Under(HandoverSituation situation) =>
        situation.Place == HandoverPlace.FromAnywhere
            ? "and it is set to complete from anywhere"
            : "and you opened the dialogue with them";

    private static string Named(HandoverSituation situation) =>
        string.IsNullOrWhiteSpace(situation.CustomerName) ? "the customer" : situation.CustomerName.Trim();

    /// <summary>
    /// The game supplies a reason with every invalid handover, but an empty one
    /// would turn the log line into a shrug. Say that instead of pretending.
    /// </summary>
    private static string InvalidReason(HandoverSituation situation) =>
        string.IsNullOrWhiteSpace(situation.HandoverChoiceInvalidReason)
            ? "it gave no reason"
            : situation.HandoverChoiceInvalidReason.Trim();
}
