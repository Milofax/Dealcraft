using System;
using System.Reflection;
using HarmonyLib;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Handover;

namespace Dealcraft;

/// <summary>
/// The two places the ledger listens, and why those two.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was checked first.</b> The spec named
/// <c>Customer.ProcessHandoverClient(Single, Boolean, String, EHandoverOutcome)</c>
/// and offered
/// <c>RpcLogic___ProcessHandoverClient_2441224929</c> as
/// the fallback. Both were read out of the game with
/// before anything was patched, and
/// neither one can carry this ledger:
/// </para>
/// <list type="number">
/// <item><description>
/// <c>ProcessHandoverClient</c> is a FishNet writer stub and nothing else. Its
/// 382 bytes are <c>get_IsServerInitialized</c>, a pooled writer, four writes
/// and <c>SendObserversRpc</c>. Both names reach the same method
/// two names — the method and
/// <c>RpcWriter___Observers_ProcessHandoverClient_2441224929</c> — which is what
/// a writer stub folded with its own body looks like.
/// </description></item>
/// <item><description>
/// Worse, <b>it is never called on the host</b>. The send was inlined into
/// <c>RpcLogic___ProcessHandoverServerSide</c>: that method reaches
/// <c>SendObserversRpc</c> directly, and its 4,171 bytes
/// contain no call to at all. A Harmony postfix there would
/// sit on a function the server-side path never enters, and would record
/// nothing, for ever, silently — the one failure mode a measuring instrument
/// must not have.
/// </description></item>
/// <item><description>
/// The string is not the one the player reads either. The interop signature
/// names it <c>npcToRecommend</c>, and <c>RpcLogic___ProcessHandoverClient</c>
/// passes it to <c>Customer.ContractWellReceived(String)</c> — it is a
/// recommended NPC, not a payment message. Nothing on that RPC carries money at
/// all: its four arguments are a satisfaction, a flag, that name and the
/// outcome.
/// </description></item>
/// </list>
/// <para>
/// <b>What is patched instead.</b> Two real methods, with real bodies, that real
/// callers reach with a real <c>call</c>:
/// </para>
/// <list type="number">
/// <item><description>
/// <b><see cref="Popup"/> — <c>DealCompletionPopup.PlayPopup</c>, the primary
/// seam.</b> It is handed
/// <c>(Customer, float satisfaction, float relationshipDelta, float basePayment,
/// List&lt;Contract.BonusPayment&gt; bonuses)</c>, and
/// <c>Contract.BonusPayment</c> carries a <c>Title</c> and an <c>Amount</c>. So
/// the five bonuses arrive already named and already split, and the ledger
/// copies them across: no string is parsed and nothing is recomputed. A row
/// reading <c>Generosity Bonus $20</c> beside <c>Curfew Bonus $200</c> settles
/// the question outright. <c>basePayment</c> is <c>Contract.Payment</c> — the
/// field at <c>+0x140</c>, loaded straight into the call,
/// which is the same offset <c>Contract::get_Payment</c> reads.
/// </description></item>
/// <item><description>
/// <b><see cref="ServerSide"/> —
/// <c>Customer.RpcLogic___ProcessHandoverServerSide_3760244802</c>
///, the second reading.</b> It carries
/// <c>totalPayment</c>, which is <c>clamp(Payment + bonusTotal, 0, max)</c> —
/// the money as the server has it — plus the satisfaction, the delivered items
/// and the contract's product list. It is reached by a genuine call from
/// <c>RpcReader___Server_ProcessHandoverServerSide</c>,
/// after that reader has checked <c>IsServerInitialized</c>. So it runs on the
/// host, once per handover, for <em>every</em> handover — the host's own, the
/// automation's, and one a remote player did by hand, which is the case the
/// popup can never see because a popup is UI and UI is local.
/// </description></item>
/// </list>
/// <para>
/// Together they are two independent readings of one quantity: the shown bonus
/// total, and <c>total_payment - contract_payment</c>. While the first ledger
/// rows are still unproven, having both is worth the second patch.
/// </para>
/// <para>
/// <b>Host only, by construction.</b> The server-side seam is the server's own
/// logic; a guest never runs it. The popup seam is local to whoever completed
/// the deal, so the recorder asks <c>InstanceFinder.IsServer</c> before it
/// writes. Nothing new is sent, no RPC is added and no game state is touched —
/// the ledger reads arguments and writes a file.
/// </para>
/// <para>
/// <b>Prefix, not postfix, on the server seam.</b> That method spends the
/// customer's goods, pays the NPC, ends the contract and files the receipt, so
/// afterwards several of the things a row wants are gone. The prefix reads them
/// while they are still there. It returns <c>void</c>, so it cannot skip the
/// original however badly it goes wrong, and every line of it is wrapped.
/// </para>
/// </remarks>
internal static class HandoverLedgerPatches
{
    /// <summary>
    /// The ledger the patches write to. Static because a Harmony patch method
    /// must be static; set once, when the feature installs the patches, and
    /// never replaced.
    /// </summary>
    private static HandoverLedger _ledger;

    /// <summary>
    /// Patch both seams. Called only when the setting is on, so a host who has
    /// not asked for a ledger has no patch installed at all.
    /// </summary>
    /// <returns>Null when both went on; otherwise what went wrong.</returns>
    public static string Install(HarmonyLib.Harmony harmony, HandoverLedger ledger)
    {
        _ledger = ledger;

        try
        {
            MethodInfo popup = AccessTools.Method(
                typeof(DealCompletionPopup),
                nameof(DealCompletionPopup.PlayPopup));

            if (popup is null)
            {
                return "DealCompletionPopup.PlayPopup was not found in the interop assemblies";
            }

            MethodInfo serverSide = AccessTools.Method(
                typeof(Customer),
                nameof(Customer.RpcLogic___ProcessHandoverServerSide_3760244802));

            if (serverSide is null)
            {
                return "Customer.RpcLogic___ProcessHandoverServerSide_3760244802 was not found "
                    + "in the interop assemblies";
            }

            harmony.Patch(popup, postfix: new HarmonyMethod(
                typeof(HandoverLedgerPatches), nameof(Popup)));

            harmony.Patch(serverSide, prefix: new HarmonyMethod(
                typeof(HandoverLedgerPatches), nameof(ServerSide)));

            return null;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    /// <summary>
    /// What the player was shown. A postfix, because the popup should have been
    /// put on screen whatever the ledger makes of it.
    /// </summary>
    public static void Popup(
        Customer customer,
        float satisfaction,
        float originalRelationshipDelta,
        float basePayment,
        Il2CppSystem.Collections.Generic.List<Contract.BonusPayment> bonuses) =>
        _ledger?.Shown(customer, satisfaction, originalRelationshipDelta, basePayment, bonuses);

    /// <summary>
    /// What the server moved. A prefix, so the contract, the goods and the
    /// world are read before this method spends them.
    /// </summary>
    public static void ServerSide(
        Customer __instance,
        HandoverScreen.EHandoverOutcome outcome,
        Il2CppSystem.Collections.Generic.List<ItemInstance> items,
        bool handoverByPlayer,
        float totalPayment,
        ProductList productList,
        float satisfaction) =>
        _ledger?.Settled(
            __instance, outcome, items, handoverByPlayer, totalPayment, productList, satisfaction);
}
