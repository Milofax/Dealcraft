using System;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class HandoverGateTests
{
    /// <summary>
    /// Everything the game said yes to. Individual tests spoil one answer at a
    /// time, so that each one states a single rule.
    /// </summary>
    private static HandoverSituation Ready() => new(
        CustomerName: "Jessi Waters",
        ContractKey: "contract-1",
        CanReachTheServer: true,
        AutomationEnabled: true,
        IsReadyForHandover: true,
        IsHandoverChoiceValid: true,
        HandoverChoiceInvalidReason: string.Empty,

        // Talking to them, on the default answer. Every test that is not about
        // the trigger is about a player who has walked over and pressed E,
        // because that is what a fresh install asks for.
        Place: HandoverPlace.WhenITalkToThem,
        TheyOpenedTheDialogue: true);

    [Fact]
    public void Hands_over_when_the_game_says_yes_to_everything()
    {
        HandoverDecision decision = HandoverGate.Decide(Ready());

        Assert.Equal(HandoverAction.HandOver, decision.Action);
    }

    /// <summary>
    /// The rule this gate used to have, stated as the rule it now has. A guest
    /// with Dealcraft hands over their own goods: the restriction was ours, not
    /// the game's — <c>Customer.ProcessHandover</c> reads
    /// <c>IsClientInitialized</c> and calls <c>SendServerRpc</c>, which is the
    /// path a guest's own Done button takes.
    /// </summary>
    [Fact]
    public void Hands_over_on_a_guest_because_the_goods_are_the_guests()
    {
        HandoverDecision decision = HandoverGate.Decide(Ready());

        Assert.Equal(HandoverAction.HandOver, decision.Action);
    }

    /// <summary>
    /// The one network condition that remains, and it is about the call rather
    /// than about hosting: without a client half <c>ProcessHandover</c> is
    /// dropped with a warning. The reason must not imply anybody has to host.
    /// </summary>
    [Fact]
    public void Stands_down_with_no_client_half_and_blames_the_connection_not_the_host()
    {
        HandoverSituation situation = Ready() with { CanReachTheServer = false };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Abstain, decision.Action);
        Assert.DoesNotContain("host", decision.Reason);
        Assert.Contains("not connected", decision.Reason);
    }

    [Fact]
    public void Stands_down_while_the_feature_is_off()
    {
        HandoverSituation situation = Ready() with { AutomationEnabled = false };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Abstain, decision.Action);
        Assert.Contains("switched off", decision.Reason);
    }

    [Fact]
    public void Refuses_and_repeats_the_reason_the_game_gave()
    {
        HandoverSituation situation = Ready() with
        {
            IsHandoverChoiceValid = false,
            HandoverChoiceInvalidReason = "Benji Coleman is handling this deal",
        };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Refuse, decision.Action);
        Assert.Contains("Benji Coleman is handling this deal", decision.Reason);
    }

    [Fact]
    public void Refuses_even_when_the_game_gave_no_reason()
    {
        HandoverSituation situation = Ready() with
        {
            IsHandoverChoiceValid = false,
            HandoverChoiceInvalidReason = "   ",
        };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Refuse, decision.Action);
        Assert.Contains("no reason", decision.Reason);
    }

    /// <summary>
    /// The deal location used to stop the handover, and the game never asked it
    /// of a player. <c>Customer.IsAtDealLocation()</c> (RVA <c>0x6AB4B0</c>) has
    /// two call sites in <c>GameAssembly.dll</c> and both are inside
    /// <c>DealerAttendDealBehaviour</c> — an NPC dealer delivering on your
    /// behalf. This is the owner's own case: walking past a customer who has not
    /// reached their spot.
    /// </summary>
    [Fact]
    public void Hands_over_to_a_customer_who_has_not_reached_the_deal_location()
    {
        // The situation no longer carries IsAtDealLocation at all; this is the
        // customer the six waited as, and the gate now answers for them.
        HandoverDecision decision = HandoverGate.Decide(Ready());

        Assert.Equal(HandoverAction.HandOver, decision.Action);
        Assert.DoesNotContain("deal location", decision.Reason);
    }

    /// <summary>
    /// The delivery window used to stop it too. <c>Customer.IsDealTime()</c>
    /// (RVA <c>0x6AB620</c>) has two call sites, both in
    /// <c>Customer.OnMinPass</c>: one feeds the customer's debug log, the other
    /// gates <c>ShouldTryGenerateDeal</c>. Neither is the handover. The game's
    /// own timing answer is <c>IsAwaitingDelivery</c>, which
    /// <c>IsReadyForHandover</c> returns.
    /// </summary>
    [Fact]
    public void Hands_over_outside_what_used_to_be_the_delivery_window()
    {
        HandoverDecision decision = HandoverGate.Decide(Ready());

        Assert.Equal(HandoverAction.HandOver, decision.Action);
        Assert.DoesNotContain("window", decision.Reason);
    }

    /// <summary>
    /// And the two together, which is how Karen Kennedy's contract sat for three
    /// minutes of the recorded session until the owner moved the clock by hand.
    /// </summary>
    [Fact]
    public void The_games_two_answers_are_the_whole_of_the_test()
    {
        HandoverDecision decision = HandoverGate.Decide(Ready());

        Assert.Equal(HandoverAction.HandOver, decision.Action);
        Assert.Contains("Complete Deal", decision.Reason);
    }

    [Fact]
    public void Waits_while_the_game_says_the_handover_is_not_ready()
    {
        HandoverSituation situation = Ready() with { IsReadyForHandover = false };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Wait, decision.Action);
        Assert.Contains("ready", decision.Reason);
    }

    [Fact]
    public void An_invalid_choice_is_refused_before_readiness_is_consulted()
    {
        // Refusing outranks waiting: a dealer-owned contract will never become
        // eligible by waiting, and the reason the game gave is worth logging now.
        HandoverSituation situation = Ready() with
        {
            IsReadyForHandover = false,
            IsHandoverChoiceValid = false,
            HandoverChoiceInvalidReason = "Benji Coleman is handling this deal",
        };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Refuse, decision.Action);
    }

    [Fact]
    public void A_contract_without_a_stable_key_is_left_alone()
    {
        HandoverSituation situation = Ready() with { ContractKey = " " };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Abstain, decision.Action);
        Assert.Contains("no contract", decision.Reason);
    }

    [Fact]
    public void The_reason_is_always_worth_logging()
    {
        HandoverDecision decision = HandoverGate.Decide(Ready());

        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    // --- the trigger: the player talking to them -----------------------------

    /// <summary>
    /// The default a fresh install carries, at the source every other default
    /// comes from: the adapter seeds its preferences entries from this object.
    /// </summary>
    [Fact]
    public void A_fresh_install_waits_for_the_player_to_talk_to_them()
    {
        Assert.Equal(HandoverPlace.WhenITalkToThem, new AdvisorSettings().Place);
        Assert.Equal(
            HandoverPlace.FromAnywhere,
            new AdvisorSettings { HandoverFromAnywhere = true }.Place);
    }

    /// <summary>
    /// Ticket 52, and the trigger the owner named: <em>"Wenn ich hingehe, dann
    /// soll halt warten, bis ich E drücke und dann den Deal machen."</em> A
    /// sweep never carries the opening, so on the default answer a sweep waits
    /// however ready the game says the customer is.
    /// </summary>
    [Fact]
    public void A_sweep_waits_because_the_player_has_not_talked_to_them()
    {
        HandoverSituation situation = Ready() with { TheyOpenedTheDialogue = false };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Wait, decision.Action);

        // Readable back by somebody who was not there: it names the customer and
        // says what it is waiting for rather than that something is wrong.
        Assert.Contains("Jessi Waters", decision.Reason);
        Assert.Contains("talk to them", decision.Reason);
    }

    /// <summary>
    /// And the opening is what lets it through, on that same pass rather than on
    /// the next sweep. This is the whole of the ticket.
    /// </summary>
    [Fact]
    public void Opening_the_dialogue_hands_over_at_once()
    {
        Assert.Equal(
            HandoverAction.HandOver,
            HandoverGate.Decide(Ready() with { TheyOpenedTheDialogue = true }).Action);
    }

    /// <summary>
    /// Which of the two answers let a handover through goes into the decision
    /// itself, so that <c>decisions.jsonl</c> can be read back by somebody who
    /// does not know what the preferences file held at the time. Under the
    /// default answer it names the dialogue opening, because the opening is what
    /// happened and the setting is only what the file held.
    /// </summary>
    [Fact]
    public void The_decision_says_which_answer_let_the_handover_through()
    {
        HandoverDecision talkedTo = HandoverGate.Decide(Ready());
        HandoverDecision anywhere = HandoverGate.Decide(
            Ready() with { Place = HandoverPlace.FromAnywhere, TheyOpenedTheDialogue = false });

        Assert.Equal(HandoverAction.HandOver, talkedTo.Action);
        Assert.Contains("you opened the dialogue with them", talkedTo.Reason);

        Assert.Equal(HandoverAction.HandOver, anywhere.Action);
        Assert.Contains("from anywhere", anywhere.Reason);
    }

    /// <summary>
    /// <em>From anywhere</em> is what the build did before this option existed,
    /// and it behaves the same: it never asks whether anybody is talking to the
    /// customer, so a sweep completes it.
    /// </summary>
    [Fact]
    public void From_anywhere_hands_over_without_anybody_talking_to_them()
    {
        HandoverSituation situation = Ready() with
        {
            Place = HandoverPlace.FromAnywhere,
            TheyOpenedTheDialogue = false,
        };

        Assert.Equal(HandoverAction.HandOver, HandoverGate.Decide(situation).Action);
    }

    /// <summary>
    /// Talking to a customer does not make a handover the game refuses valid, and
    /// not talking to one does not turn a refusal into waiting. The order is the
    /// one the gate already had: refusing outranks waiting.
    /// </summary>
    [Fact]
    public void A_refusal_outranks_the_trigger_in_both_directions()
    {
        HandoverSituation situation = Ready() with
        {
            TheyOpenedTheDialogue = false,
            IsHandoverChoiceValid = false,
            HandoverChoiceInvalidReason = "Benji Coleman is handling this deal",
        };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Refuse, decision.Action);
        Assert.Contains("Benji Coleman", decision.Reason);
    }

    /// <summary>
    /// And the game's own readiness answer is asked before the trigger, because a
    /// customer the game does not report as ready is not made ready by being
    /// talked to. This is the case the owner's session ran into: at 20:39:07Z
    /// <c>IsReadyForHandover(true)</c> was false, and the reason the record kept
    /// says so rather than blaming the player.
    /// </summary>
    [Fact]
    public void The_games_own_readiness_is_asked_before_the_trigger()
    {
        HandoverSituation situation = Ready() with { IsReadyForHandover = false };

        HandoverDecision decision = HandoverGate.Decide(situation);

        Assert.Equal(HandoverAction.Wait, decision.Action);
        Assert.Contains("ready to hand over", decision.Reason);
    }

    /// <summary>
    /// The switch still outranks everything: talking to a customer with the
    /// handover on the game's own behaviour does nothing at all.
    /// </summary>
    [Fact]
    public void Talking_to_them_does_nothing_while_the_handover_is_switched_off()
    {
        HandoverSituation situation = Ready() with { AutomationEnabled = false };

        Assert.Equal(HandoverAction.Abstain, HandoverGate.Decide(situation).Action);
    }

    /// <summary>
    /// Ticket 52 removed the proximity gate rather than leaving it unreachable,
    /// and a deletion stays deleted only if something fails when it comes back.
    /// </summary>
    /// <remarks>
    /// The reading behind it was sound and is still in
    /// <c>docs/handover-truth.md</c>; what went is the mod holding a distance at
    /// all, and with it the one figure in this feature that was never read out of
    /// the binary — the mod runs no raycast, so it measured root to root. A
    /// worker who adds a distance back is re-opening that, and the ticket says
    /// why it is not wanted: a distance was a proxy for intent and the owner has
    /// named the intent.
    /// </remarks>
    [Fact]
    public void The_gate_holds_no_distance_and_is_not_given_one()
    {
        string[] asked = typeof(HandoverSituation)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(asked, name => name.Contains("Metres", StringComparison.Ordinal));
        Assert.DoesNotContain(asked, name => name.Contains("Distance", StringComparison.Ordinal));
    }
}
