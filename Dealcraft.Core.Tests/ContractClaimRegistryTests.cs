using System;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class ContractClaimRegistryTests
{
    [Fact]
    public void A_contract_nobody_has_touched_can_be_claimed()
    {
        var registry = new ContractClaimRegistry();

        ClaimDecision decision = registry.Claim("contract-1");

        Assert.Equal(ClaimOutcome.Claimed, decision.Outcome);
    }

    /// <summary>
    /// The case the whole registry exists for: six players' clients, or one
    /// client on six consecutive frames, all asking about the same contract.
    /// The second ask is answered, not punished — a throw here would mean a
    /// try/catch on the hot path of every contract scan.
    /// </summary>
    [Fact]
    public void Claiming_the_same_contract_twice_reports_it_as_already_claimed()
    {
        var registry = new ContractClaimRegistry();
        registry.Claim("contract-1");

        ClaimDecision decision = registry.Claim("contract-1");

        Assert.Equal(ClaimOutcome.AlreadyClaimed, decision.Outcome);
    }

    /// <summary>
    /// Releasing says "the automation's turn on this contract is over", not
    /// "start again". While the game still lists the contract, a second claim
    /// is refused — otherwise one over-eager release would send a second
    /// counter-offer to the same customer, which is the one thing this registry
    /// exists to prevent.
    /// </summary>
    [Fact]
    public void A_released_contract_the_game_still_lists_is_not_claimed_again()
    {
        var registry = new ContractClaimRegistry();
        registry.Claim("contract-1");
        registry.Release("contract-1");

        ClaimDecision decision = registry.Claim("contract-1");

        Assert.Equal(ClaimOutcome.AlreadyClaimed, decision.Outcome);
    }

    [Fact]
    public void A_contract_a_human_has_touched_is_refused()
    {
        var registry = new ContractClaimRegistry();

        registry.NoteHumanInteraction("contract-1");

        Assert.Equal(ClaimOutcome.LockedByHuman, registry.Claim("contract-1").Outcome);
    }

    /// <summary>
    /// A player opening a contract the automation is already holding takes it
    /// over. The mod does not get to finish what it started.
    /// </summary>
    [Fact]
    public void A_human_takes_a_contract_away_from_a_claim_already_held()
    {
        var registry = new ContractClaimRegistry();
        registry.Claim("contract-1");

        registry.NoteHumanInteraction("contract-1");

        Assert.Equal(ClaimOutcome.LockedByHuman, registry.Claim("contract-1").Outcome);
    }

    /// <summary>
    /// Releasing must not launder a human lock away. If it did, any adapter
    /// that releases on a contract state change would hand the contract back to
    /// the automation the player just took off it.
    /// </summary>
    [Fact]
    public void Releasing_a_human_locked_contract_leaves_it_locked()
    {
        var registry = new ContractClaimRegistry();
        registry.Claim("contract-1");
        registry.NoteHumanInteraction("contract-1");

        registry.Release("contract-1");

        Assert.Equal(ClaimOutcome.LockedByHuman, registry.Claim("contract-1").Outcome);
    }

    /// <summary>
    /// After a save/load the registry is empty and is rebuilt from whatever the
    /// game now says is live; nothing about claims is written into the save. A
    /// contract the game has stopped listing is genuinely over, so the entry
    /// goes, and a contract that later comes back under the same key is a new
    /// one and may be claimed.
    /// </summary>
    [Fact]
    public void Reconcile_forgets_contracts_the_game_no_longer_lists()
    {
        var registry = new ContractClaimRegistry();
        registry.Claim("gone");
        registry.Claim("still-here");

        registry.Reconcile(new[] { "still-here" });

        Assert.Equal(ClaimOutcome.Claimed, registry.Claim("gone").Outcome);
        Assert.Equal(ClaimOutcome.AlreadyClaimed, registry.Claim("still-here").Outcome);
    }

    /// <summary>
    /// Reconcile rebuilds from the live contracts, and a human lock on one of
    /// them is part of what has to survive that rebuild. Otherwise every load,
    /// and every routine trim, would hand the player's contract back to the
    /// automation.
    /// </summary>
    [Fact]
    public void Reconcile_keeps_the_human_lock_on_a_contract_that_is_still_live()
    {
        var registry = new ContractClaimRegistry();
        registry.NoteHumanInteraction("contract-1");

        registry.Reconcile(new[] { "contract-1" });

        Assert.Equal(ClaimOutcome.LockedByHuman, registry.Claim("contract-1").Outcome);
    }

    /// <summary>
    /// "For the rest of its life" ends when the life does. A later contract
    /// reusing the key is a different deal and the player has not touched it.
    /// </summary>
    [Fact]
    public void Reconcile_forgets_the_human_lock_once_the_contract_is_gone()
    {
        var registry = new ContractClaimRegistry();
        registry.NoteHumanInteraction("contract-1");

        registry.Reconcile(new string[0]);

        Assert.Equal(ClaimOutcome.Claimed, registry.Claim("contract-1").Outcome);
    }

    /// <summary>
    /// The caller supplies the key. When it cannot build a stable one — a
    /// customer that despawned mid-scan, a contract with no identity yet — it
    /// passes what it has, and the registry refuses rather than filing every
    /// nameless contract under one entry and answering only the first of them.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_contract_without_a_stable_key_is_not_eligible(string? contractKey)
    {
        var registry = new ContractClaimRegistry();

        ClaimDecision decision = registry.Claim(contractKey!);

        Assert.Equal(ClaimOutcome.NotEligible, decision.Outcome);
    }

    /// <summary>
    /// Hours of play are thousands of contracts. With the game's live set fed
    /// back in each pass, what the registry holds tracks what is on the map and
    /// does not grow with the length of the session.
    /// </summary>
    [Fact]
    public void A_long_session_of_contracts_coming_and_going_does_not_accumulate()
    {
        var registry = new ContractClaimRegistry();
        var highWaterMark = 0;

        for (var round = 0; round < 500; round++)
        {
            string[] live = { $"contract-{round}-a", $"contract-{round}-b" };

            foreach (string key in live)
            {
                registry.Claim(key);
            }

            registry.NoteHumanInteraction(live[0]);
            registry.Release(live[1]);
            registry.Reconcile(live);

            highWaterMark = Math.Max(highWaterMark, registry.Count);
        }

        Assert.Equal(2, highWaterMark);

        registry.Reconcile(new string[0]);
        Assert.Equal(0, registry.Count);
    }

    /// <summary>
    /// Reconcile is what keeps the registry small, so a caller that stops
    /// reconciling has a bug. The registry holds a hard ceiling anyway and
    /// refuses new contracts at it. It refuses rather than evicting: dropping a
    /// claim to make room would let the automation act on that contract a
    /// second time, which is worse than not acting on a new one.
    /// </summary>
    [Fact]
    public void A_registry_that_is_never_reconciled_stops_at_its_ceiling()
    {
        var registry = new ContractClaimRegistry(capacity: 3);
        registry.Claim("contract-1");
        registry.Claim("contract-2");
        registry.Claim("contract-3");

        ClaimDecision decision = registry.Claim("contract-4");

        Assert.Equal(ClaimOutcome.NotEligible, decision.Outcome);
        Assert.Equal(3, registry.Count);
        Assert.Equal(ClaimOutcome.AlreadyClaimed, registry.Claim("contract-1").Outcome);
    }

    /// <summary>
    /// The ceiling holds back the automation, never the player. A human lock is
    /// recorded whatever the registry's size, because the alternative is the
    /// mod countering a deal somebody has open.
    /// </summary>
    [Fact]
    public void A_full_registry_still_records_a_human_lock()
    {
        var registry = new ContractClaimRegistry(capacity: 1);
        registry.Claim("contract-1");

        registry.NoteHumanInteraction("contract-2");

        Assert.Equal(ClaimOutcome.LockedByHuman, registry.Claim("contract-2").Outcome);
    }

    /// <summary>
    /// The acceptance criterion from the ticket, stated at the seam: six
    /// observers of one customer's offer, one counter-offer sent. Calls arrive
    /// one after another on the game's main thread, so counting grants in a
    /// plain loop is the honest simulation.
    /// </summary>
    [Fact]
    public void Six_observers_of_one_contract_produce_one_claim()
    {
        var registry = new ContractClaimRegistry();
        var granted = 0;

        for (var observer = 0; observer < 6; observer++)
        {
            if (registry.Claim("contract-1").Outcome == ClaimOutcome.Claimed)
            {
                granted++;
            }
        }

        Assert.Equal(1, granted);
    }

    /// <summary>
    /// The other half of that, verified rather than assumed, because the
    /// handover stopped being host-only and somebody will ask.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The registry is one dictionary on one instance. Two players each running
    /// Dealcraft have two of them, and the second knows nothing about the
    /// first's claim — so this prevents a double pass <b>within</b> an instance
    /// and not across them. That is the design and not a gap: for the two shared
    /// passes it never arises, because a guest's gate stands them down before a
    /// claim is ever attempted; and for the handover it does not need to,
    /// because two players cannot both be the one carrying the goods.
    /// </para>
    /// <para>
    /// If they somehow are — both standing at one customer with stock — the
    /// game settles it exactly as it settles two players racing the Done button
    /// today: <c>Customer.IsHandoverChoiceValid</c> and the vanilla server RPC.
    /// Nothing of ours has to.
    /// </para>
    /// </remarks>
    [Fact]
    public void Two_instances_each_claim_because_the_registry_is_per_instance()
    {
        var mine = new ContractClaimRegistry();
        var theirs = new ContractClaimRegistry();

        Assert.Equal(ClaimOutcome.Claimed, mine.Claim("contract-1").Outcome);
        Assert.Equal(ClaimOutcome.Claimed, theirs.Claim("contract-1").Outcome);

        // And within either one, still exactly once.
        Assert.NotEqual(ClaimOutcome.Claimed, mine.Claim("contract-1").Outcome);
        Assert.NotEqual(ClaimOutcome.Claimed, theirs.Claim("contract-1").Outcome);
    }

    /// <summary>
    /// Every outcome ends up in the mod's log, so every outcome has to say
    /// something a reader can act on.
    /// </summary>
    [Fact]
    public void Every_decision_carries_a_reason_fit_for_a_log_line()
    {
        var registry = new ContractClaimRegistry(capacity: 2);
        registry.NoteHumanInteraction("locked");

        ClaimDecision[] decisions =
        {
            registry.Claim("contract-1"),
            registry.Claim("contract-1"),
            registry.Claim("locked"),
            registry.Claim("one-too-many"),
            registry.Claim(""),
        };

        Assert.Equal(
            new[]
            {
                ClaimOutcome.Claimed,
                ClaimOutcome.AlreadyClaimed,
                ClaimOutcome.LockedByHuman,
                ClaimOutcome.NotEligible,
                ClaimOutcome.NotEligible,
            },
            Array.ConvertAll(decisions, decision => decision.Outcome));

        foreach (ClaimDecision decision in decisions)
        {
            Assert.False(string.IsNullOrWhiteSpace(decision.Reason), $"{decision.Outcome} has no reason");
        }
    }

    /// <summary>
    /// The adapter calls these from game callbacks, where a thrown exception
    /// takes the patch down with it. A key it could not build is nothing to
    /// record, so it is nothing to complain about either.
    /// </summary>
    [Fact]
    public void Releasing_or_locking_a_contract_without_a_key_does_nothing()
    {
        var registry = new ContractClaimRegistry();

        registry.Release(null!);
        registry.Release("  ");
        registry.NoteHumanInteraction(null!);
        registry.NoteHumanInteraction("  ");

        Assert.Equal(0, registry.Count);
    }

    /// <summary>
    /// A decision nobody set — an unfilled array slot, a field never assigned —
    /// must not read as permission to act. The safe outcome is the zero value.
    /// </summary>
    [Fact]
    public void A_decision_that_was_never_made_grants_nothing()
    {
        ClaimDecision decision = default;

        Assert.Equal(ClaimOutcome.NotEligible, decision.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }
}
