using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// A player drops out of the session and comes back. The host carries on, and
/// nothing fires twice when the observer returns.
///
/// What makes that true is that a client never acts at all. There is no
/// "disconnect" for the mod to handle, because there is nothing running on a
/// guest's machine to be interrupted — every gate answers "the host's" on any
/// machine that is not the server, whether it has just joined, just come back,
/// or been there all along. These tests hold that line at all three gates, so a
/// future change that lets one of them act on a client fails here rather than in
/// somebody's session.
/// </summary>
public class ClientReconnectTests
{
    /// <summary>
    /// The returning observer. Their client receives the same replicated
    /// customer state the host has, and if the scheduler acted on it the
    /// customer would be accepted twice.
    /// </summary>
    [Fact]
    public void A_client_that_reconnects_does_not_schedule_the_offer_it_now_sees()
    {
        ScheduleDecision decision = DealScheduler.Consider(
            Standing(), ServerAuthority.NotTheServer, automationOn: true);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("not the server", decision.Reason);
    }

    [Fact]
    public void A_client_that_reconnects_does_not_counter_the_offer_it_now_sees()
    {
        CounterofferDecision decision = CounterofferGate.Consider(
            Standing(), ServerAuthority.NotTheServer, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("not the server", decision.Reason);
    }

    /// <summary>
    /// The handover is the exception, and deliberately. A guest who reconnects
    /// and is standing in front of their customer with the goods hands them
    /// over: they are the one carrying them. Two players cannot both be, so
    /// there is nothing here for a host rule to prevent.
    /// </summary>
    [Fact]
    public void A_guest_that_reconnects_hands_over_the_deal_it_is_standing_in_front_of()
    {
        HandoverDecision decision = HandoverGate.Decide(Ready());

        Assert.Equal(HandoverAction.HandOver, decision.Action);
    }

    /// <summary>
    /// A guest with a live client half is still not allowed to answer the shared
    /// conversation or write the shared contract list. The asymmetry is the
    /// whole rule, so both halves of it are held here.
    /// </summary>
    [Fact]
    public void A_guest_still_neither_counters_nor_schedules()
    {
        CounterofferDecision countered = CounterofferGate.Consider(
            Standing(), ServerAuthority.Guest, automationOn: true);
        ScheduleDecision scheduled = DealScheduler.Consider(
            Standing(), ServerAuthority.Guest, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, countered.Outcome);
        Assert.Contains("not the server", countered.Reason);
        Assert.Equal(ScheduleOutcome.Skip, scheduled.Outcome);
        Assert.Contains("not the server", scheduled.Reason);
    }

    /// <summary>
    /// The host's side of the same moment. A guest leaving does not take the
    /// host's authority with it — the host is still the server and still has its
    /// own client half — so the deal the guest walked away from still gets
    /// handed over.
    /// </summary>
    [Fact]
    public void The_host_carries_on_while_a_guest_is_away()
    {
        Assert.Equal(HandoverAction.HandOver, HandoverGate.Decide(Ready()).Action);
        Assert.Equal(
            ScheduleOutcome.Claim,
            DealScheduler.Consider(
                Standing(), ServerAuthority.Held, automationOn: true).Outcome);
    }

    /// <summary>
    /// A contract the host already acted on stays acted-on across the churn. The
    /// registry is keyed on the contract and reconciled against the game's live
    /// contracts, and neither of those has anything to do with who is connected
    /// — which is exactly why a reconnect cannot make the automation act twice.
    /// </summary>
    [Fact]
    public void A_contract_the_host_has_claimed_survives_a_guest_leaving_and_returning()
    {
        var registry = new ContractClaimRegistry();
        string contract = ContractKey.ForOffer("npc-jessi", elapsedDays: 4, timeOfDay: 2330);
        registry.Claim(contract);

        // A guest leaves; the host's sweep comes round. The live list is the
        // game's contracts, not the game's players, so it is unchanged.
        registry.Reconcile(new[] { contract });

        // The guest returns and their client sees the same customer. The host's
        // next sweep asks again.
        registry.Reconcile(new[] { contract });

        Assert.Equal(ClaimOutcome.AlreadyClaimed, registry.Claim(contract).Outcome);
    }

    /// <summary>
    /// A player who took a contract by hand keeps it while they are away. Coming
    /// back to find the mod had countered their deal the moment they dropped
    /// would be worse than the mod never having been installed.
    /// </summary>
    [Fact]
    public void A_contract_a_player_took_by_hand_is_still_theirs_when_they_return()
    {
        var registry = new ContractClaimRegistry();
        string contract = ContractKey.ForOffer("npc-jessi", elapsedDays: 4, timeOfDay: 2330);
        registry.NoteHumanInteraction(contract);

        registry.Reconcile(new[] { contract });

        Assert.Equal(ClaimOutcome.LockedByHuman, registry.Claim(contract).Outcome);
    }

    /// <summary>
    /// A late joiner, at the gate. Nothing about the mod runs on their machine,
    /// so what they see of an already-scheduled deal is whatever the game
    /// replicated to them — which is an ordinary deal, because an ordinary deal
    /// is the only thing the automation ever created.
    ///
    /// The second half of that is a property of the vanilla RPCs rather than of
    /// this code, and it is one of the criteria that can only be confirmed with
    /// a real client in a real session.
    /// </summary>
    [Fact]
    public void A_late_joiner_runs_none_of_the_automation()
    {
        var conditions = new LifecycleConditions
        {
            Pass = LifecyclePass.Scheduling,
            Enabled = true,
            Authority = ServerAuthority.NotTheServer,
            SaveLoaded = true,
            };

        Assert.Equal(LifecycleOutcome.StandDown, LifecycleGate.Consider(conditions).Outcome);
        Assert.Equal(
            LifecycleOutcome.StandDown,
            LifecycleGate.Consider(conditions with { Pass = LifecyclePass.Handover }).Outcome);
        Assert.Equal(
            LifecycleOutcome.StandDown,
            LifecycleGate.Consider(conditions with { Pass = LifecyclePass.Prices }).Outcome);
    }

    private static PendingOffer Standing() =>
        new("npc-jessi#4:2330", "Jessi Waters", hasOfferedContract: true, alreadyOnADeal: false);

    private static HandoverSituation Ready() => new(
        CustomerName: "Jessi Waters",
        ContractKey: "contract-1",
        CanReachTheServer: true,
        AutomationEnabled: true,
        IsReadyForHandover: true,
        IsHandoverChoiceValid: true,
        HandoverChoiceInvalidReason: string.Empty,

        // Talking to them: this file is about the connection coming back, not
        // about what triggered the handover.
        Place: HandoverPlace.WhenITalkToThem,
        TheyOpenedTheDialogue: true);
}
