namespace Dealcraft.Core;

/// <summary>
/// Everything the gate is allowed to know about one pending handover. Plain
/// values only: two of them are the game's own answers, read off the customer
/// by the adapter and passed through unchanged.
/// <para>
/// It used to carry <c>IsAtDealLocation</c> and <c>IsDealTime</c> as well.
/// Neither is a question the game asks before it offers <i>Complete Deal</i> —
/// see <see cref="HandoverGate"/> — so neither is read any more. A value nobody
/// consults is not a spare part; it is a rule waiting to come back.
/// </para>
/// <para>
/// It used to carry a distance too, and that is gone as well. Ticket 49 read the
/// game's own proximity threshold and gated on it; ticket 52 replaced the proxy
/// with the thing it stood for. What is here now is
/// <see cref="TheyOpenedTheDialogue"/>, which is not a reading of the world but
/// the report of an event that has just happened.
/// </para>
/// </summary>
/// <param name="CustomerName">
/// The full name the game spells for them, for the log and for the gate's own
/// sentences. There is no stable customer id.
/// </param>
/// <param name="ContractKey">
/// What <see cref="ContractClaimRegistry"/> keys this contract on. Blank means
/// the adapter could not identify the contract, and nothing may be done to it.
/// </param>
/// <param name="CanReachTheServer">
/// Whether this machine's client half is up for this customer —
/// <c>Customer.IsClientInitialized</c>. Not a question about hosting:
/// <c>Customer.ProcessHandover</c> writes through the client and is dropped with
/// a warning without one, on the host exactly as on a guest.
/// <para>
/// There is deliberately no "am I the host" here any more. Two players cannot
/// both be the one carrying the goods, so the conditions below are the whole of
/// the rule — see <see cref="HandoverGate"/>.
/// </para>
/// </param>
/// <param name="AutomationEnabled">The host's AutoHandover switch.</param>
/// <param name="IsReadyForHandover">
/// <c>Customer.IsReadyForHandover(true)</c>, which is the whole of the game's own
/// test for putting <i>Complete Deal</i> on the prompt. The <c>true</c> is not a
/// choice of ours: it is the dialogue choice's <c>Enabled</c> flag, which
/// <c>Customer.SetUpDialogue</c> sets to <c>true</c> and never lowers, and the
/// method returns <c>false</c> outright for <c>false</c>. It consults
/// <c>Player.Local</c>, so on every machine it answers about that machine's own
/// player — which is exactly what makes it the right question for a per-player
/// handover. Passed through as the game gave it.
/// </param>
/// <param name="IsHandoverChoiceValid">
/// <c>Customer.IsHandoverChoiceValid(out reason)</c> — the same dialogue choice's
/// validity check, which is what decides whether pressing <i>Complete Deal</i>
/// does anything rather than naming a dealer.
/// </param>
/// <param name="HandoverChoiceInvalidReason">
/// The reason the game gave when it said no. Repeated verbatim in the log; the
/// mod never substitutes a reason of its own for one the game supplied.
/// </param>
/// <param name="Place">
/// The player's answer to <i>when may this happen</i>. Not a reading of the
/// game: the two values and the default are <see cref="HandoverPlace"/>'s.
/// </param>
/// <param name="TheyOpenedTheDialogue">
/// Whether the gate is being asked because the player has just opened the
/// dialogue with this customer, rather than because the sweep came round.
/// <para>
/// <b>It is an event and not a state, and that is the whole point of it.</b> The
/// sweep passes <c>false</c> always, because "is a dialogue open right now" is
/// not the question — the question is "did one just open", and six seconds of a
/// dialogue standing open with nothing happening is precisely the failure ticket
/// 52 exists to end. The adapter sets it from a Harmony prefix on
/// <c>DialogueController.Interacted()</c> (RVA <c>0x6DCE70</c>), which is the
/// method that opens it.
/// </para>
/// </param>
public sealed record HandoverSituation(
    string CustomerName,
    string ContractKey,
    bool CanReachTheServer,
    bool AutomationEnabled,
    bool IsReadyForHandover,
    bool IsHandoverChoiceValid,
    string HandoverChoiceInvalidReason,
    HandoverPlace Place,
    bool TheyOpenedTheDialogue);
