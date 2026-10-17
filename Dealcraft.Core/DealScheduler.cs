using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Decides whether an offer may be auto-accepted and which window it goes into.
/// Pure: no game types, no clock, no I/O.
/// </summary>
/// <remarks>
/// The decision arrives in two halves because claiming a contract is not a
/// question, it is an act. <see cref="Consider"/> answers everything that can be
/// asked without side effects and ends at
/// <see cref="ScheduleOutcome.Claim"/>; the caller then claims the contract
/// through <see cref="ContractClaimRegistry"/> and brings the answer back to
/// <see cref="ChooseWindow"/>. Nothing is claimed for an offer that was never
/// going to be scheduled.
/// </remarks>
public static class DealScheduler
{
    /// <summary>
    /// Everything that can be decided about an offer before touching anything.
    /// </summary>
    /// <param name="offer">The standing offer, read off the customer.</param>
    /// <param name="authority">Whether this machine may act at all.</param>
    /// <param name="automationOn">
    /// Whether scheduling may act at all, which is now whether the host allows
    /// any window. There is no switch above the four windows: four ticked boxes
    /// already say which windows are allowed, and none ticked says none.
    /// </param>
    public static ScheduleDecision Consider(
        in PendingOffer offer,
        ServerAuthority authority,
        bool automationOn)
    {
        // First, so that a host who never turned scheduling on gets a quiet log
        // instead of a running commentary about customers they did not ask
        // about.
        if (!automationOn)
        {
            return ScheduleDecision.Skip("deal scheduling is switched off");
        }

        switch (authority)
        {
            // Both, and not one condition: a guest with a live client half
            // could make this call and must not. One shared conversation, one
            // shared contract list — six installs must not answer them six
            // times. Only the handover is per player; see HandoverGate.
            case ServerAuthority.NotTheServer:
            case ServerAuthority.Guest:
                return ScheduleDecision.Skip(
                    "this machine is not the server, so the host decides this one");
            case ServerAuthority.WithoutAClient:
                return ScheduleDecision.Skip(
                    "the server is not connected as a client yet, and the accept call needs that");
        }

        if (!offer.HasOfferedContract)
        {
            return ScheduleDecision.Skip($"{Who(offer)} has no offer on the table");
        }

        if (offer.AlreadyOnADeal)
        {
            return ScheduleDecision.Skip($"{Who(offer)} is already on a deal");
        }

        // There is no exclusion list any more, and it is not a regression: the
        // game has a per-customer opt-out of its own. Assigning the customer to
        // a dealer makes the game's own pass route the offer to the dealer
        // instead of notifying the player, so no offer from that customer ever
        // reaches this gate. Ticket 37 read that out; app.md records the caveat,
        // which is that the game's control reads as "give this customer away"
        // rather than "leave them alone".
        if (string.IsNullOrWhiteSpace(offer.ContractKey))
        {
            return ScheduleDecision.Skip(
                $"{Who(offer)}'s offer has no stable key, so it cannot be claimed");
        }

        return ScheduleDecision.Claim($"{Who(offer)}'s offer is eligible for scheduling");
    }

    /// <summary>
    /// Turn a claim into a window, or into the reason there is none.
    /// </summary>
    /// <param name="claim">
    /// What <see cref="ContractClaimRegistry.Claim"/> answered. Only
    /// <see cref="ClaimOutcome.Claimed"/> is permission to act; the others are
    /// reported as they were given, so a contract a player opened by hand says
    /// exactly that rather than blaming the window set.
    /// </param>
    /// <param name="allowed">The windows the host permits.</param>
    /// <param name="onOffer">The windows the game is currently offering.</param>
    /// <param name="now">
    /// The window the game's clock is in, so the choice is the soonest allowed
    /// window from now rather than the first one in the list. Null when the
    /// game could not be asked.
    /// </param>
    /// <param name="rotation">Picks the soonest allowed window from now.</param>
    public static ScheduleDecision ChooseWindow(
        ClaimDecision claim,
        DealWindowSet allowed,
        IReadOnlyCollection<DealWindow> onOffer,
        DealWindow? now,
        DealWindowRotation rotation)
    {
        if (claim.Outcome != ClaimOutcome.Claimed)
        {
            return ScheduleDecision.Skip(claim.Reason);
        }

        return rotation.Choose(allowed, onOffer, now);
    }

    private static string Who(in PendingOffer offer) =>
        string.IsNullOrWhiteSpace(offer.CustomerName) ? "the customer" : offer.CustomerName;
}
