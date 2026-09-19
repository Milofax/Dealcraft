using System;
using System.Reflection;
using HarmonyLib;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.NPCs;

namespace Dealcraft;

/// <summary>
/// The one place the handover listens for the player saying <i>this one, now</i>:
/// a prefix on the method that opens a customer's dialogue.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a patch and not a finer scan.</b> The owner's own session settles it.
/// <c>decisions.jsonl</c> has Dealcraft waiting on Peter File at 20:39:05Z
/// because the game answered <c>IsReadyForHandover(true)</c> false, and at
/// 20:39:07Z still false; by the time it was true he had pressed <c>E</c>, seen
/// nothing happen, and finished the deal by hand — which locks the automation out
/// of that contract for the rest of the session. The gap between the game
/// offering <i>Complete Deal</i> and the player pressing it is shorter than any
/// sweep interval worth running. It is an event, so it is listened for.
/// </para>
/// <para>
/// <b><see cref="Interacted"/> —
/// <c>ScheduleOne.Dialogue.DialogueController.Interacted()</c>.</b> It is the method that opens the dialogue.
/// Read out of the game with
///: it calls
/// <c>DialogueController.GetActiveChoices()</c>, at
/// it branches on that list's <c>Count</c>
/// (<c>cmp 0x18(%rax)</c>, <c>jg</c>), and only on the non-empty side does it
/// reach <c>DialogueHandler.StartDialogue(DialogueContainer, Boolean, String)</c>
///. With no choices it takes the other side and plays a
/// worldspace one-liner instead (<c>5.0f</c>, virtual call
///) — no dialogue screen. So this method running and the
/// game deciding whether <i>Complete Deal</i> is on the prompt are one event,
/// and <c>GetActiveChoices</c> is the same method the project notes
/// already establishes as the one that decides it.
/// </para>
/// <para>
/// <b>It is reached by real calls and is not inlined.</b>
/// <c>DialogueController.Start()</c> calls
/// <c>Component::GetComponent()</c>, builds delegates and calls
/// <c>UnityEvent::AddListener(UnityAction)</c>, which is how the customer's
/// <c>InteractableObject</c> reaches it; and
/// <c>DialogueController.StartGenericDialogue(Boolean)</c>
/// reaches with a plain <c>call</c>. A detour at the entry
/// catches both. The type declares exactly one <c>Interacted()</c>, it is not
/// virtual, and nothing under <c>ScheduleOne.Dialogue.DialogueController</c>
/// overrides it — so one patch catches every customer.
/// </para>
/// <para>
/// <b>Prefix, not postfix, and returning <c>void</c>.</b> Handing over before
/// the original runs means <c>Customer.ProcessHandover</c> has already lowered
/// <c>IsAwaitingDelivery</c> (<c>+0x138</c>, written false at
///) by the time <c>GetActiveChoices()</c> is called, so
/// <i>Complete Deal</i> is not on the list the player is shown and there is
/// nothing left for him to click. That is the ticket's acceptance criterion
/// rather than a race against his hand. Returning <c>void</c> means this cannot
/// skip the original however badly it goes wrong, and every line of it is
/// wrapped.
/// </para>
/// <para>
/// <b>Nothing new is sent and no game state is touched here.</b> The prefix reads
/// an argument, finds a customer and hands both to
/// <see cref="AutomaticHandover.WhenTheyAreTalkedTo"/>, which asks the same
/// lifecycle guard and the same gate the sweep asks. Local to whoever pressed
/// <c>E</c>, which is what a handover is.
/// </para>
/// </remarks>
internal static class HandoverDialoguePatches
{
    /// <summary>
    /// Who is told. Static because a Harmony patch method must be static; set
    /// once, when the feature installs the patch, and never replaced.
    /// </summary>
    private static Action<Customer> _opened;

    /// <summary>
    /// Patch the seam. Called only once the handover has been read as automated,
    /// so a player who has not asked for one has no detour on the game's
    /// dialogue at all.
    /// </summary>
    /// <returns>Null when it went on; otherwise what went wrong.</returns>
    public static string Install(HarmonyLib.Harmony harmony, Action<Customer> opened)
    {
        _opened = opened;

        try
        {
            MethodInfo interacted = AccessTools.Method(
                typeof(DialogueController),
                nameof(DialogueController.Interacted));

            if (interacted is null)
            {
                return "DialogueController.Interacted was not found in the interop assemblies";
            }

            harmony.Patch(interacted, prefix: new HarmonyMethod(
                typeof(HandoverDialoguePatches), nameof(Interacted)));

            return null;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    /// <summary>
    /// The player has pressed <c>E</c> on an NPC that talks. If that NPC is a
    /// customer, the handover hears about it before the dialogue is built.
    /// </summary>
    public static void Interacted(DialogueController __instance)
    {
        try
        {
            if (_opened is null || __instance == null)
            {
                return;
            }

            Customer customer = CustomerOf(__instance.npc);
            if (customer != null)
            {
                _opened(customer);
            }
        }
        catch (Exception)
        {
            // Swallowed here and nowhere else: this sits inside the game's own
            // interaction handling, and a dialogue that fails to open because a
            // mod threw is a broken game rather than a missed deal. What goes
            // wrong inside the handover is reported by the handover, which wraps
            // itself for the same reason.
        }
    }

    /// <summary>
    /// The customer this NPC is, or null for an NPC who is not one.
    /// </summary>
    /// <remarks>
    /// <b>The roster rather than the components.</b> <c>Customer.SetUpDialogue</c>
    /// does call <c>Component::GetComponent()</c>, which
    /// suggests <c>Customer</c> and <c>DialogueController</c> share a GameObject,
    /// but the generic argument is not named in the reading and nothing here
    /// needs to assume a component layout. <c>Customer.UnlockedCustomers</c> is
    /// the same list <see cref="AutomaticHandover"/>'s sweep walks, so the two
    /// cannot come to disagree about which customers exist — and it is walked
    /// once per <c>E</c> press rather than once per frame.
    /// </remarks>
    private static Customer CustomerOf(NPC npc)
    {
        if (npc == null)
        {
            return null;
        }

        Il2CppSystem.Collections.Generic.List<Customer> customers = Customer.UnlockedCustomers;
        if (customers is null)
        {
            return null;
        }

        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];
            if (customer != null && customer.NPC == npc)
            {
                return customer;
            }
        }

        return null;
    }
}
