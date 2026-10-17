using System;
using System.Reflection;
using HarmonyLib;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;

namespace Dealcraft;

/// <summary>
/// The two places the ledger listens, and why those two.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not the client RPC.</b> The spec named
/// <c>Customer.ProcessHandoverClient</c>, which now takes
/// <c>(Single, Boolean, String)</c>. Nothing on it carries money: its arguments
/// are a satisfaction, a flag and the name of an NPC the customer recommends,
/// which <c>RpcLogic___ProcessHandoverClient</c> passes to
/// <c>Customer.ContractWellReceived(String)</c>. A ledger listening
/// there would record a handover without its payment.
/// </para>
/// <para>
/// <b>What is patched.</b> Two methods the game runs when a handover completes:
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
/// the question outright. <c>basePayment</c> is <c>Contract.Payment</c>, passed
/// straight through.
/// </description></item>
/// <item><description>
/// <b><see cref="ServerSide"/> —
/// <c>Customer.RpcLogic___ProcessHandoverServerSide_3315874220</c>, the second
/// reading.</b> It carries
/// <c>totalPayment</c>, which is <c>clamp(Payment + bonusTotal, 0, max)</c> —
/// the money as the server has it — plus the satisfaction, the delivered items
/// and the contract's product list.
/// <c>RpcReader___Server_ProcessHandoverServerSide</c> calls it after checking
/// <c>IsServerInitialized</c>. So it runs on the
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
/// delivered product, takes the NPC's cash and files the receipt, so
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
                nameof(Customer.RpcLogic___ProcessHandoverServerSide_3315874220));

            if (serverSide is null)
            {
                return "Customer.RpcLogic___ProcessHandoverServerSide_3315874220 was not found "
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
        Il2CppSystem.Collections.Generic.List<ItemInstance> items,
        bool handoverByPlayer,
        float totalPayment,
        ProductList productList,
        float satisfaction) =>
        _ledger?.Settled(
            __instance, items, handoverByPlayer, totalPayment, productList, satisfaction);
}
