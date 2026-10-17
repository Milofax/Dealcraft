using System.Collections.Generic;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The rule ticket 06 is about, stated as a session rather than as a call: one
/// contract, several machines and several passes, and exactly one counter-offer
/// comes out of it.
/// </summary>
public class CounterofferOncePerContractTests
{
    private const string Contract = "npc-1#3:1200";

    private static PendingOffer TheOffer => new(Contract, "Jessi Waters", true, false);

    private static PlannedOffer Planned => PlannedOffer.Offer(8, 1350f, "best of the curve");

    /// <summary>
    /// Six players, each with the mod installed, each seeing the same
    /// replicated customer. Five of them are not the server and never get as
    /// far as claiming anything; the host claims once and sends once.
    /// </summary>
    [Fact]
    public void Six_machines_holding_one_contract_send_one_counter_offer()
    {
        var authorities = new List<ServerAuthority> { ServerAuthority.Held };
        for (int guest = 0; guest < 5; guest++)
        {
            authorities.Add(ServerAuthority.NotTheServer);
        }

        int sent = 0;
        foreach (ServerAuthority authority in authorities)
        {
            // Each machine has a registry of its own: nothing about a claim is
            // replicated, which is precisely why the server gate has to do the
            // work of deciding who acts.
            if (Counter(new ContractClaimRegistry(), authority, out _))
            {
                sent++;
            }
        }

        Assert.Equal(1, sent);
    }

    /// <summary>
    /// The sweep comes round every few seconds for as long as the offer is on
    /// the table. It claims on the first pass and is refused on every one after.
    /// </summary>
    [Fact]
    public void Sweeping_the_same_contract_again_sends_nothing_more()
    {
        var claims = new ContractClaimRegistry();

        Assert.True(Counter(claims, ServerAuthority.Held, out _));

        for (int sweep = 0; sweep < 10; sweep++)
        {
            Assert.False(Counter(claims, ServerAuthority.Held, out CounterofferDecision again));
            Assert.Equal(CounterofferOutcome.Skip, again.Outcome);
        }
    }

    /// <summary>
    /// The contract stays claimed while the offer is live, so releasing it
    /// after the counter has gone out does not open the door to a second one.
    /// </summary>
    [Fact]
    public void Releasing_a_countered_contract_does_not_let_it_be_countered_again()
    {
        var claims = new ContractClaimRegistry();

        Assert.True(Counter(claims, ServerAuthority.Held, out _));
        claims.Release(Contract);

        Assert.False(Counter(claims, ServerAuthority.Held, out _));
    }

    /// <summary>
    /// A player opening the offer takes it out of the automation's hands for
    /// good — not until the next sweep, and not until the next reconcile.
    /// </summary>
    [Fact]
    public void A_contract_a_player_opened_is_left_alone_for_the_rest_of_its_life()
    {
        var claims = new ContractClaimRegistry();
        claims.NoteHumanInteraction(Contract);

        for (int sweep = 0; sweep < 10; sweep++)
        {
            Assert.False(Counter(claims, ServerAuthority.Held, out CounterofferDecision decision));
            Assert.Contains("by hand", decision.Reason);

            // Reconciling is what keeps a long session's memory flat. It must
            // not be what forgets that a player owns this one.
            claims.Reconcile(new[] { Contract });
        }
    }

    /// <summary>
    /// The registry is in memory and starts empty after a load. What stops the
    /// reloaded session countering the same offer again is the game's own flag,
    /// which the server set on every observer and the save kept.
    /// </summary>
    [Fact]
    public void A_reload_does_not_counter_an_offer_that_was_already_countered()
    {
        var beforeTheReload = new ContractClaimRegistry();
        Assert.True(Counter(beforeTheReload, ServerAuthority.Held, out _));

        var afterTheReload = new ContractClaimRegistry();
        var countered = new PendingOffer(Contract, "Jessi Waters", true, false, alreadyCountered: true);

        CounterofferDecision decision = CounterofferGate.Consider(
            countered, ServerAuthority.Held, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Equal(0, afterTheReload.Count);
    }

    /// <summary>
    /// One machine's pass over one contract, exactly as the loop runs it:
    /// consider, claim, decide. Answers whether a counter-offer went out.
    /// </summary>
    private static bool Counter(
        ContractClaimRegistry claims,
        ServerAuthority authority,
        out CounterofferDecision decision)
    {
        var settings = new AdvisorSettings();

        decision = CounterofferGate.Consider(TheOffer, authority, automationOn: true);
        if (decision.Outcome != CounterofferOutcome.Claim)
        {
            return false;
        }

        decision = CounterofferGate.Send(
            claims.Claim(Contract),
            Planned,
            chance: 0.9f,
            offeredPrice: 900f,
            floor: settings.AcceptanceProbabilityThreshold);
        return decision.Outcome == CounterofferOutcome.Send;
    }
}
