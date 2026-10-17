using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class CounterofferGateTests
{
    /// <summary>
    /// The floor the player has not set. Every test below that is about the
    /// money rather than about the floor passes it, so that the two arguments
    /// for refusing a counter stay separable: a threshold of zero is the honest
    /// way to say "whatever they will take".
    /// </summary>
    private const float NoFloor = 0f;

    private static PendingOffer AnOffer(
        string key = "npc-1#3:1200",
        string name = "Jessi Waters",
        bool hasOffer = true,
        bool onADeal = false,
        bool countered = false) =>
        new(key, name, hasOffer, onADeal, countered);

    private static PlannedOffer Planned(int quantity = 8, float total = 1350f) =>
        PlannedOffer.Offer(quantity, total, "best of the curve");

    [Fact]
    public void An_ordinary_offer_on_the_host_is_worth_claiming()
    {
        CounterofferDecision decision = CounterofferGate.Consider(
            AnOffer(), ServerAuthority.Held, automationOn: true);

        Assert.Equal(CounterofferOutcome.Claim, decision.Outcome);
    }

    /// <summary>
    /// Off by default is the spec's rule for every automation switch, and the
    /// switch is read before anything else so that a host who never turned it
    /// on gets a quiet log rather than a running commentary.
    /// </summary>
    [Fact]
    public void Nothing_happens_while_the_switch_is_off()
    {
        CounterofferDecision decision = CounterofferGate.Consider(
            AnOffer(), ServerAuthority.Held, automationOn: false);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("switched off", decision.Reason);
    }

    [Fact]
    public void A_guest_never_counters_anything()
    {
        CounterofferDecision decision = CounterofferGate.Consider(
            AnOffer(), ServerAuthority.NotTheServer, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("not the server", decision.Reason);
    }

    /// <summary>
    /// The RPC is written by the client half of the host, which the shipped
    /// code checks before it touches the writer. Without one the call would be
    /// dropped with a warning, so it waits instead.
    /// </summary>
    [Fact]
    public void A_server_whose_client_half_is_not_up_waits()
    {
        CounterofferDecision decision = CounterofferGate.Consider(
            AnOffer(), ServerAuthority.WithoutAClient, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
    }

    [Fact]
    public void A_customer_with_no_offer_on_the_table_is_left_alone()
    {
        CounterofferDecision decision = CounterofferGate.Consider(
            AnOffer(hasOffer: false), ServerAuthority.Held, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("no offer", decision.Reason);
    }

    [Fact]
    public void A_customer_already_on_a_deal_is_left_alone()
    {
        CounterofferDecision decision = CounterofferGate.Consider(
            AnOffer(onADeal: true), ServerAuthority.Held, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("already on a deal", decision.Reason);
    }

    /// <summary>
    /// The game's own flag, set on every observer by the vanilla RPC and
    /// carried in the save. It is what stops a reloaded session from countering
    /// an offer that was already countered, whoever countered it.
    /// </summary>
    [Fact]
    public void An_offer_that_has_already_been_countered_is_left_alone()
    {
        CounterofferDecision decision = CounterofferGate.Consider(
            AnOffer(countered: true), ServerAuthority.Held, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("already been countered", decision.Reason);
    }

    [Fact]
    public void An_offer_with_no_stable_key_cannot_be_claimed_and_so_is_left_alone()
    {
        CounterofferDecision decision = CounterofferGate.Consider(
            AnOffer(key: "  "), ServerAuthority.Held, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("stable key", decision.Reason);
    }

    [Fact]
    public void A_counter_worth_more_than_the_standing_offer_is_sent()
    {
        // 0.9 x 1350 = 1215, against 900 on the table.
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(),
            chance: 0.9f,
            offeredPrice: 900f,
            floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Send, decision.Outcome);
        Assert.Equal(8, decision.Quantity);
        Assert.Equal(1350f, decision.TotalPrice);
    }

    /// <summary>
    /// The defect this rule exists for, at the shipped defaults. A standing
    /// offer of $100, a curve that finds $110 at 0.9 confidence. The old rule
    /// compared the $10 gain against MinimumCounterGain's $1 bar and sent it.
    /// Its expected value is $99 — and sending clears the standing offer's
    /// responses, so there is no way back to the $100 that was certain.
    /// </summary>
    [Fact]
    public void The_counter_that_used_to_lose_a_dollar_is_now_refused()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(total: 110f),
            chance: 0.9f,
            offeredPrice: 100f, floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("99", decision.Reason);
    }

    [Fact]
    public void The_same_counter_is_sent_once_it_is_likely_enough_to_pay()
    {
        // 0.95 x 110 = 104.50, which beats the 100 on the table.
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(total: 110f),
            chance: 0.95f,
            offeredPrice: 100f, floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Send, decision.Outcome);
    }

    /// <summary>
    /// A bigger ask does not make up for a worse chance. This is the whole of
    /// why the gain comparison was wrong: it could not see the second number.
    /// </summary>
    [Fact]
    public void A_much_bigger_counter_at_a_much_worse_chance_is_refused()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(total: 1800f),
            chance: 0.4f,
            offeredPrice: 900f, floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
    }

    [Fact]
    public void A_certain_counter_is_judged_on_the_money_alone()
    {
        Assert.Equal(
            CounterofferOutcome.Send,
            CounterofferGate.Send(
                ClaimDecision.Claimed("c"),
                Planned(total: 901f),
                chance: 1f,
                offeredPrice: 900f,
                floor: NoFloor).Outcome);

        Assert.Equal(
            CounterofferOutcome.Skip,
            CounterofferGate.Send(
                ClaimDecision.Claimed("c"),
                Planned(total: 900f),
                chance: 1f,
                offeredPrice: 900f,
                floor: NoFloor).Outcome);
    }

    /// <summary>
    /// A chance the search could not answer for is not a reason to gamble the
    /// standing offer. Nothing times anything is nothing.
    /// </summary>
    [Fact]
    public void A_counter_with_no_chance_behind_it_is_never_sent()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(),
            chance: 0f,
            offeredPrice: 900f,
            floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
    }

    /// <summary>
    /// Only a claim is permission to act, and the claim's own reason is carried
    /// through — so a contract a player opened by hand says exactly that rather
    /// than blaming the price.
    /// </summary>
    [Fact]
    public void An_offer_a_player_has_touched_is_never_sent()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.LockedByHuman("a player opened it by hand"),
            Planned(),
            chance: 0.9f,
            offeredPrice: 900f, floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("opened it by hand", decision.Reason);
    }

    [Fact]
    public void An_offer_claimed_by_an_earlier_pass_is_not_sent_again()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.AlreadyClaimed("already claimed"),
            Planned(),
            chance: 0.9f,
            offeredPrice: 900f,
            floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
    }

    [Fact]
    public void Nothing_is_sent_when_the_curve_recommended_nothing()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            PlannedOffer.None("no price at any quantity reached the confidence the search asked for"),
            chance: 0.9f,
            offeredPrice: 900f, floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("reached the confidence", decision.Reason);
    }

    [Fact]
    public void A_counter_below_the_standing_offer_is_never_sent()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(total: 500f),
            chance: 1f,
            offeredPrice: 900f,
            floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
    }

    /// <summary>
    /// Countering at exactly the offered price spends the one counteroffer the
    /// game allows for no money, however certain it is.
    /// </summary>
    [Fact]
    public void A_counter_at_the_offered_price_is_never_sent()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(total: 900f),
            chance: 1f,
            offeredPrice: 900f,
            floor: NoFloor);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
    }

    // --- the player's floor ---------------------------------------------------

    /// <summary>
    /// The control's whole job: a counter the search says is worth making, and
    /// a player who does not want to gamble that hard. The money argument
    /// passes — 0.7 × 1350 is 945 against 900 on the table — and the floor
    /// refuses it anyway.
    /// </summary>
    [Fact]
    public void A_counter_below_the_floor_is_refused_however_well_it_pays()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(),
            chance: 0.7f,
            offeredPrice: 900f,
            floor: 0.9f);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("70%", decision.Reason);
        Assert.Contains("90%", decision.Reason);
    }

    /// <summary>
    /// And the other side of it: the floor changes nothing about the counters
    /// that clear it. The same call with the same numbers and a floor the chance
    /// meets is the decision the search asked for.
    /// </summary>
    [Fact]
    public void A_counter_that_clears_the_floor_is_the_decision_the_search_asked_for()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(),
            chance: 0.9f,
            offeredPrice: 900f,
            floor: 0.9f);

        Assert.Equal(CounterofferOutcome.Send, decision.Outcome);
        Assert.Equal(1350f, decision.TotalPrice);
    }

    /// <summary>
    /// A chance sitting exactly on the floor clears it: the player asked to
    /// never offer <em>below</em> this chance.
    /// </summary>
    [Fact]
    public void A_chance_exactly_on_the_floor_is_not_below_it()
    {
        Assert.Equal(
            CounterofferOutcome.Send,
            CounterofferGate.Send(
                ClaimDecision.Claimed("c"),
                Planned(total: 1000f),
                chance: 0.95f,
                offeredPrice: 900f,
                floor: 0.95f).Outcome);
    }

    /// <summary>
    /// The mechanical consequence the review found, and the reason neither side
    /// of the comparison may go through <c>Figures.Fraction</c> or
    /// <c>Figures.Percent</c>: the game's own figure can exceed 1, and a chance
    /// of 101% satisfies a floor of 100%. Clamped, the best offers of all would
    /// be refused for exceeding the top of the control.
    /// </summary>
    [Fact]
    public void A_chance_above_certainty_satisfies_a_floor_of_certainty()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(total: 1000f),
            chance: 1.01f,
            offeredPrice: 900f,
            floor: ChanceFloor.Highest);

        Assert.Equal(CounterofferOutcome.Send, decision.Outcome);
    }

    /// <summary>
    /// The floor refuses; it never picks. The same winning offer at the same
    /// chance against two different floors: one refuses it, the other sends it
    /// exactly as the search proposed it. There is no way from here to reach for
    /// a different rung, and the gate does not pretend there is.
    /// </summary>
    [Fact]
    public void The_floor_refuses_the_winner_rather_than_choosing_another()
    {
        CounterofferDecision refused = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(total: 1800f),
            chance: 0.6f,
            offeredPrice: 900f,
            floor: 0.7f);

        CounterofferDecision sent = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(total: 1800f),
            chance: 0.6f,
            offeredPrice: 900f,
            floor: ChanceFloor.Lowest);

        Assert.Equal(CounterofferOutcome.Skip, refused.Outcome);
        Assert.Equal(CounterofferOutcome.Send, sent.Outcome);
        Assert.Equal(8, sent.Quantity);
        Assert.Equal(1800f, sent.TotalPrice);
    }

    /// <summary>
    /// The floor is read before the money, so a player who set one is told
    /// about their own setting rather than about an expected value they would
    /// have to work out to recognise.
    /// </summary>
    [Fact]
    public void A_counter_that_fails_both_tests_is_refused_in_the_players_own_terms()
    {
        CounterofferDecision decision = CounterofferGate.Send(
            ClaimDecision.Claimed("claimed"),
            Planned(total: 910f),
            chance: 0.5f,
            offeredPrice: 900f,
            floor: 0.9f);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Contains("you set", decision.Reason);
    }
}
