namespace Dealcraft.Core;

/// <summary>
/// When the automation may complete a handover.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="WhenITalkToThem"/> is the default, and the trigger is the
/// player opening the dialogue with that customer.</b> Pressing <c>E</c> is the
/// player saying <i>this one, now</i>; the mod has no better signal and does not
/// need one. <c>DialogueController.Interacted()</c> is the
/// method that opens it: it calls <c>GetActiveChoices()</c>, and reaches
/// <c>DialogueHandler.StartDialogue</c> only when that
/// list is non-empty (<c>cmp 0x18(%rax)</c>, <c>jg</c>).
/// So the moment this value names is the same moment the game decides whether
/// <i>Complete Deal</i> is on the prompt. the project notes carries
/// the choice's own conditions.
/// </para>
/// <para>
/// <b>It replaces a distance, and the distance is gone rather than unreachable.</b>
/// Ticket 49 read the game's own proximity threshold —
/// <c>InteractionManager.CheckHover</c> against the
/// object's own <c>MaxInteractionRange</c> — and gated on it. That reading was
/// sound and is still in the project notes, but a distance was only
/// ever a proxy for intent, and the owner has named the intent. A condition we
/// no longer need is not one we keep.
/// </para>
/// <para>
/// <b><see cref="FromAnywhere"/> is what the build did before this option
/// existed</b>, and it is a capability the owner wanted kept rather than the
/// behaviour he wanted. With it on, the deal completes on the next sweep after
/// its time arrives and the goods leave the inventory across the map.
/// </para>
/// <para>
/// <b>In multiplayer the difference is not a preference.</b> The contract list
/// is shared, and a handover takes from the local player's own inventory
/// (<c>HandoverGoods</c>) and is paid to the local player
/// (<c>Contract.SubmitPayment</c>). A host on
/// <see cref="FromAnywhere"/> therefore completes every contract on that shared
/// list out of his own pockets, from across the map, <b>whatever the other
/// players chose</b> — a guest on <see cref="WhenITalkToThem"/> or on manual
/// finds the deal already gone. With <see cref="WhenITalkToThem"/> that cannot
/// happen, because talking to the customer is what decides who serves them. The
/// cost was put to the owner and he kept the answer with the warning on it;
/// the project notes records the decision.
/// </para>
/// </remarks>
public enum HandoverPlace
{
    /// <summary>
    /// The player has opened the dialogue with this customer. The zero value on
    /// purpose: a value nobody set must be the careful one.
    /// </summary>
    WhenITalkToThem,

    /// <summary>Wherever the player happens to be, on the next sweep.</summary>
    FromAnywhere,
}
