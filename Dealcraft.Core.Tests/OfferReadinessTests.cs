using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Whether a customer has finished talking, and what to do about one who never
/// will. The automation answers a conversation a person is reading, so it waits
/// for the last message; and it may not lose an offer to a chain that never ends,
/// so the wait has a limit and the limit has an answer.
/// </summary>
public class OfferReadinessTests
{
    private static readonly ConversationReading Finished =
        new(exists: true, rollingOut: false, acceptOffered: true);

    private static readonly ConversationReading Silent =
        new(exists: true, rollingOut: false, acceptOffered: false);

    private static readonly ConversationReading Stuck =
        new(exists: true, rollingOut: true, acceptOffered: true);

    private static readonly ConversationReading CounterGap =
        new(exists: true, rollingOut: false, acceptOffered: false, playerSpokeLast: true);

    [Fact]
    public void A_customer_who_has_finished_and_offered_the_reply_is_answered_in_the_conversation()
    {
        Assert.Equal(OfferReadinessOutcome.Reply, OfferReadiness.Consider(Finished, 0f).Outcome);
    }

    /// <summary>
    /// The case the player reported. The reply buttons arrive with the first
    /// message, so seeing them says nothing about the last one.
    /// </summary>
    [Fact]
    public void Reply_buttons_on_screen_do_not_mean_the_customer_has_finished()
    {
        var talking = new ConversationReading(exists: true, rollingOut: true, acceptOffered: true);

        OfferReadinessDecision decision = OfferReadiness.Consider(talking, 3f);

        Assert.Equal(OfferReadinessOutcome.Wait, decision.Outcome);
        Assert.Contains("still", decision.Reason);
    }

    /// <summary>
    /// A counter-offer clears the replies and the customer's answer comes back a
    /// moment later. The accept reads its reply's text, so taking the deal in
    /// that gap throws, and the claim it spent keeps the deal from being taken at
    /// all; the offer then stands until it expires.
    /// </summary>
    [Fact]
    public void An_offer_whose_replies_are_gone_for_a_moment_is_waited_for()
    {
        var answering = new ConversationReading(exists: true, rollingOut: false, acceptOffered: false);

        Assert.Equal(OfferReadinessOutcome.Wait, OfferReadiness.Consider(answering, 0f).Outcome);
    }

    [Fact]
    public void A_counter_offer_the_customer_is_still_answering_is_waited_for()
    {
        var answering = new ConversationReading(exists: true, rollingOut: true, acceptOffered: false);

        Assert.Equal(OfferReadinessOutcome.Wait, OfferReadiness.Consider(answering, 1f).Outcome);
    }

    /// <summary>
    /// A customer without a conversation cannot be answered in one, and the
    /// game's accept reply throws without one, so there is nothing to wait for.
    /// </summary>
    [Fact]
    public void A_customer_without_a_conversation_is_served_directly_and_at_once()
    {
        var none = new ConversationReading(exists: false, rollingOut: false, acceptOffered: false);

        Assert.Equal(OfferReadinessOutcome.AcceptDirectly, OfferReadiness.Consider(none, 0f).Outcome);
    }

    /// <summary>
    /// An offer loaded from a save replays nothing, and the game restores its
    /// replies with it, so it is ready the moment it is seen.
    /// </summary>
    [Fact]
    public void A_saved_offer_with_its_replies_is_answered_at_once()
    {
        Assert.Equal(OfferReadinessOutcome.Reply, OfferReadiness.Consider(Finished, 0f).Outcome);
    }

    /// <summary>
    /// A save written between a counter-offer and its answer holds the offer and
    /// no replies, and the answer died with the session. No reply is ever coming,
    /// and the offer must still be served rather than expire.
    /// </summary>
    [Fact]
    public void A_saved_offer_without_replies_is_served_directly_once_the_wait_runs_out()
    {
        var silent = new ConversationReading(exists: true, rollingOut: false, acceptOffered: false);

        Assert.Equal(
            OfferReadinessOutcome.Wait,
            OfferReadiness.Consider(silent, OfferReadiness.PatienceSeconds - 1f).Outcome);
        Assert.Equal(
            OfferReadinessOutcome.AcceptDirectly,
            OfferReadiness.Consider(silent, OfferReadiness.PatienceSeconds).Outcome);
    }

    /// <summary>
    /// A chain that never reports its end must not hold the deal back either.
    /// The reply is there, so the deal is answered the normal way.
    /// </summary>
    [Fact]
    public void A_chain_that_never_ends_is_answered_in_the_conversation_once_the_wait_runs_out()
    {
        var stuck = new ConversationReading(exists: true, rollingOut: true, acceptOffered: true);

        Assert.Equal(
            OfferReadinessOutcome.Reply,
            OfferReadiness.Consider(stuck, OfferReadiness.PatienceSeconds).Outcome);
    }

    /// <summary>
    /// The reviewed case. An offer parked for minutes with nowhere to go has not
    /// been waiting on anybody, so the moment it is countered is the start of the
    /// wait and not the end of it. Taken here it goes on the old terms, and the
    /// customer's answer then lands on an offer that is gone.
    /// </summary>
    [Fact]
    public void An_offer_parked_for_minutes_is_not_taken_in_the_gap_of_a_counter_offer()
    {
        var patience = new OfferPatience();

        Assert.Equal(OfferReadinessOutcome.Reply, patience.Consider("offer", Finished, 0f).Outcome);
        Assert.Equal(OfferReadinessOutcome.Reply, patience.Consider("offer", Finished, 300f).Outcome);

        Assert.Equal(OfferReadinessOutcome.Wait, patience.Consider("offer", CounterGap, 305f).Outcome);
    }

    [Fact]
    public void Time_the_customer_had_already_answered_is_not_counted()
    {
        var patience = new OfferPatience();

        patience.Consider("offer", Silent, 0f);
        patience.Consider("offer", Finished, 20f);

        Assert.Equal(OfferReadinessOutcome.Wait, patience.Consider("offer", Silent, 25f).Outcome);
        Assert.Equal(OfferReadinessOutcome.Wait, patience.Consider("offer", Silent, 54f).Outcome);
    }

    /// <summary>
    /// A wait already running on something else does not carry into a counter-
    /// offer: the customer's answer gets the whole wait to itself.
    /// </summary>
    [Fact]
    public void A_counter_offer_restarts_a_wait_that_was_nearly_over()
    {
        var patience = new OfferPatience();

        patience.Consider("offer", Silent, 0f);

        Assert.Equal(OfferReadinessOutcome.Wait, patience.Consider("offer", CounterGap, 29f).Outcome);
        Assert.Equal(OfferReadinessOutcome.Wait, patience.Consider("offer", CounterGap, 58f).Outcome);
    }

    /// <summary>
    /// The contract key survives a counter-offer, so the clock must not. The
    /// answer arriving is a new thing to wait for, not more of the old one.
    /// </summary>
    [Fact]
    public void An_answered_counter_offer_starts_its_own_wait()
    {
        var patience = new OfferPatience();
        var answering = new ConversationReading(
            exists: true, rollingOut: true, acceptOffered: true, countered: true);

        patience.Consider("offer", Stuck, 0f);

        Assert.Equal(OfferReadinessOutcome.Wait, patience.Consider("offer", answering, 29f).Outcome);
        Assert.Equal(OfferReadinessOutcome.Wait, patience.Consider("offer", answering, 58f).Outcome);
    }

    /// <summary>
    /// An answer takes a tick or two, so one missing for the whole wait is not on
    /// its way any more: the session it was sent in is gone.
    /// </summary>
    [Fact]
    public void A_counter_offer_nobody_answered_for_the_whole_wait_is_served_directly()
    {
        var patience = new OfferPatience();

        patience.Consider("offer", CounterGap, 100f);

        Assert.Equal(
            OfferReadinessOutcome.AcceptDirectly,
            patience.Consider("offer", CounterGap, 100f + OfferReadiness.PatienceSeconds).Outcome);
    }

    [Fact]
    public void A_customer_without_a_conversation_costs_no_wait()
    {
        var patience = new OfferPatience();
        var none = new ConversationReading(exists: false, rollingOut: false, acceptOffered: false);

        Assert.Equal(OfferReadinessOutcome.AcceptDirectly, patience.Consider("offer", none, 0f).Outcome);
    }

    [Fact]
    public void Forgetting_an_offer_starts_its_wait_again()
    {
        var patience = new OfferPatience();

        patience.Consider("offer", Silent, 0f);
        patience.Forget("offer");

        Assert.Equal(OfferReadinessOutcome.Wait, patience.Consider("offer", Silent, 40f).Outcome);
    }

    [Fact]
    public void Offers_no_longer_live_are_dropped_and_live_ones_keep_their_wait()
    {
        var patience = new OfferPatience();

        patience.Consider("gone", Silent, 0f);
        patience.Consider("kept", Silent, 0f);
        patience.Retain(new[] { "kept" });

        Assert.Equal(OfferReadinessOutcome.Wait, patience.Consider("gone", Silent, 40f).Outcome);
        Assert.Equal(OfferReadinessOutcome.AcceptDirectly, patience.Consider("kept", Silent, 40f).Outcome);
    }

    [Fact]
    public void The_gap_of_a_counter_offer_is_the_players_word_with_no_reply_up()
    {
        Assert.True(CounterGap.CounterOnItsWay);
        Assert.False(Silent.CounterOnItsWay);
        Assert.False(Finished.CounterOnItsWay);

        Assert.False(new ConversationReading(
            exists: true, rollingOut: false, acceptOffered: false, countered: true, playerSpokeLast: true)
            .CounterOnItsWay);
    }

    /// <summary>
    /// The zero value of the enum is the answer that spends nothing, so a decision
    /// nobody filled in cannot take a deal by accident.
    /// </summary>
    [Fact]
    public void An_unset_decision_waits()
    {
        Assert.Equal(OfferReadinessOutcome.Wait, default(OfferReadinessDecision).Outcome);
    }
}
