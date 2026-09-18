using System.Collections.Generic;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Save the game, quit to the desktop, start it again, load: no contract is
/// processed a second time.
///
/// The claim registry is deliberately not written into the save, so after a
/// restart it is empty and cannot be what stops the repeat. Something else has
/// to, and these tests say what: the game's own state. A deal that was accepted
/// is a deal the customer is on; an offer that was countered carries the game's
/// <c>IsCounterOffer</c> flag, which the server replicated and the save kept. The
/// mod reads both back rather than remembering anything.
///
/// Written as a before-and-after so the reload is visible in the test rather
/// than implied by it.
/// </summary>
public class SaveAndReloadTests
{
    /// <summary>
    /// The claim that has to be true for the rest of these to matter: what the
    /// registry holds comes from the live contracts and nowhere else. Loading is
    /// modelled as what it is — a new registry, because a restart is a new
    /// process — and the only thing that puts anything back into it is the
    /// game's list.
    /// </summary>
    [Fact]
    public void After_a_restart_the_registry_starts_empty_and_is_rebuilt_from_the_game()
    {
        var beforeTheSave = new ContractClaimRegistry();
        beforeTheSave.Claim("npc-jessi#4:2330");
        beforeTheSave.NoteHumanInteraction("npc-benji#4:1800");
        Assert.Equal(2, beforeTheSave.Count);

        // Quit and start again. Nothing crosses this line: the registry is not
        // in the save, and there is no file for it to come back from.
        var afterTheLoad = new ContractClaimRegistry();
        Assert.Equal(0, afterTheLoad.Count);

        // The first sweep of the loaded save reconciles against what the game
        // reports, which is where everything the registry knows comes from.
        afterTheLoad.Reconcile(new[] { "npc-jessi#4:2330" });
        Assert.Equal(0, afterTheLoad.Count);
    }

    /// <summary>
    /// The scheduling half. A deal accepted before the save is a contract the
    /// customer is running after the load, and the scheduler refuses a customer
    /// who is already on a deal — so the empty registry never gets the chance to
    /// let it through.
    /// </summary>
    [Fact]
    public void A_deal_accepted_before_the_save_is_not_scheduled_again_after_the_load()
    {
        var registry = new ContractClaimRegistry();

        // Before: an offer on the table, claimed, accepted.
        var onTheTable = new PendingOffer(
            "npc-jessi#4:2330", "Jessi Waters", hasOfferedContract: true, alreadyOnADeal: false);
        Assert.Equal(ScheduleOutcome.Claim, Consider(onTheTable).Outcome);
        Assert.Equal(ClaimOutcome.Claimed, registry.Claim(onTheTable.ContractKey).Outcome);

        // After the load: no offer on the table any more, and a contract
        // running. This is the customer as the game reports them, not as the
        // mod remembers them.
        var running = new PendingOffer(
            "npc-jessi#4:2330", "Jessi Waters", hasOfferedContract: false, alreadyOnADeal: true);

        ScheduleDecision decision = Consider(running);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("no offer on the table", decision.Reason);
    }

    /// <summary>
    /// The counter-offer half, and the harder one: a countered offer is still an
    /// offer on the table after the load, so nothing about "has an offer" saves
    /// us. The game's own <c>IsCounterOffer</c> does, and it is in the save
    /// because the server set it through the vanilla
    /// <c>SetContractIsCounterOffer</c> RPC.
    /// </summary>
    [Fact]
    public void An_offer_countered_before_the_save_is_not_countered_again_after_the_load()
    {
        var countered = new PendingOffer(
            "npc-jessi#4:2330",
            "Jessi Waters",
            hasOfferedContract: true,
            alreadyOnADeal: false,
            alreadyCountered: true);

        CounterofferDecision decision = CounterofferGate.Consider(
            countered, ServerAuthority.Held, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("already been countered", decision.Reason);
    }

    /// <summary>
    /// The other side of the same coin, so that "nothing is re-processed" is not
    /// bought by processing nothing. An offer that was on the table when the
    /// save was written and was never acted on is still work, and after the load
    /// it gets done.
    /// </summary>
    [Fact]
    public void An_offer_that_was_never_acted_on_is_still_worked_after_the_load()
    {
        var untouched = new PendingOffer(
            "npc-benji#4:1800", "Benji Coleman", hasOfferedContract: true, alreadyOnADeal: false);

        Assert.Equal(ScheduleOutcome.Claim, Consider(untouched).Outcome);
        Assert.Equal(
            CounterofferOutcome.Claim,
            CounterofferGate.Consider(
                untouched, ServerAuthority.Held, automationOn: true).Outcome);
    }

    /// <summary>
    /// The reconcile the ticket asks to see proved, in the shape a load puts it
    /// in: the contracts the save came back with are kept, everything the sweep
    /// was holding from before is dropped. A human lock on a contract that is
    /// still there survives, because a player who took a contract off the
    /// automation did not hand it back by saving.
    /// </summary>
    [Fact]
    public void Reconciling_after_a_load_drops_the_dead_and_keeps_a_live_human_lock()
    {
        var registry = new ContractClaimRegistry();
        registry.Claim("expired-while-away");
        registry.Claim("still-standing");
        registry.NoteHumanInteraction("a-player-has-this-one");

        registry.Reconcile(new[] { "still-standing", "a-player-has-this-one" });

        Assert.Equal(ClaimOutcome.Claimed, registry.Claim("expired-while-away").Outcome);
        Assert.Equal(ClaimOutcome.AlreadyClaimed, registry.Claim("still-standing").Outcome);
        Assert.Equal(ClaimOutcome.LockedByHuman, registry.Claim("a-player-has-this-one").Outcome);
    }

    /// <summary>
    /// The gate is asked before any of the above happens, and during a load
    /// there is no save to speak of yet. A pass that read a half-built world
    /// would be the most expensive way to be early.
    /// </summary>
    [Fact]
    public void Nothing_is_worked_in_the_seconds_before_the_save_is_loaded()
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(new LifecycleConditions
        {
            Pass = LifecyclePass.Scheduling,
            Enabled = true,
            Authority = ServerAuthority.Held,
            SaveLoaded = false,
            });

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
    }

    private static ScheduleDecision Consider(in PendingOffer offer) =>
        DealScheduler.Consider(
            offer, ServerAuthority.Held, automationOn: true);
}
