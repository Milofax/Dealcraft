using System;
using System.Collections.Generic;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class DealWindowRotationTests
{
    private static DealWindowSet Allowing(params DealWindow[] windows)
    {
        var set = new DealWindowSet();
        foreach (DealWindow window in windows)
        {
            set.Allow(window, true);
        }

        return set;
    }

    /// <summary>All four are offerable unless a test says otherwise.</summary>
    private static readonly IReadOnlyCollection<DealWindow> AnyWindow = DealWindowSet.All;

    /// <summary>
    /// The clock most of these tests stand at. Morning is the window the old
    /// walk always landed in, so starting there is where the old and the new
    /// answers agree and a test about something else is not also a test about
    /// the clock.
    /// </summary>
    private const DealWindow AtMorning = DealWindow.Morning;

    [Fact]
    public void One_allowed_window_is_the_one_that_is_picked()
    {
        var rotation = new DealWindowRotation();

        ScheduleDecision decision = rotation.Choose(
            Allowing(DealWindow.Night), AnyWindow, AtMorning);

        Assert.Equal(ScheduleOutcome.Schedule, decision.Outcome);
        Assert.Equal(DealWindow.Night, decision.Window);
    }

    /// <summary>
    /// Exactly one window allowed is the original's behaviour, and a valid
    /// choice: every deal goes into it, however many there are.
    /// </summary>
    [Fact]
    public void With_one_window_allowed_every_deal_goes_into_it()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(DealWindow.LateNight);

        DealWindow[] chosen = Enumerable.Range(0, 5)
            .Select(_ => rotation.Choose(allowed, AnyWindow, AtMorning).Window)
            .ToArray();

        Assert.Equal(Enumerable.Repeat(DealWindow.LateNight, 5), chosen);
    }

    /// <summary>
    /// The difference from the original that the ticket asks for: with several
    /// windows allowed, every deal goes into the soonest one from now that the
    /// game is offering. The owner threw out the spreading this class used to
    /// do: the game does not say which region a deal is in, so travel cannot be
    /// planned and scattering deals buys nothing.
    /// </summary>
    [Fact]
    public void With_several_allowed_every_deal_takes_the_soonest_of_them()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(DealWindow.Morning, DealWindow.Night);

        DealWindow[] chosen = Enumerable.Range(0, 4)
            .Select(_ => rotation.Choose(allowed, AnyWindow, AtMorning).Window)
            .ToArray();

        // Night, not Morning: the clock is in Morning and the current window is
        // not offered to a deal any more.
        Assert.Equal(
            new[] { DealWindow.Night, DealWindow.Night, DealWindow.Night, DealWindow.Night },
            chosen);
    }

    /// <summary>
    /// The choice holds no state, so the same inputs give the same window every
    /// time and a reload changes nothing.
    /// </summary>
    [Fact]
    public void Four_allowed_windows_still_give_the_same_one_every_time()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(
            DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight);

        DealWindow[] chosen = Enumerable.Range(0, 5)
            .Select(_ => rotation.Choose(allowed, AnyWindow, DealWindow.Afternoon).Window)
            .ToArray();

        Assert.All(chosen, window => Assert.Equal(DealWindow.Night, window));
    }

    /// <summary>
    /// None allowed is a real answer, not a silent one: the scheduler stands
    /// down and the reason goes in the log.
    /// </summary>
    [Fact]
    public void With_nothing_allowed_nothing_is_scheduled_and_the_reason_says_why()
    {
        var rotation = new DealWindowRotation();

        ScheduleDecision decision = rotation.Choose(new DealWindowSet(), AnyWindow, AtMorning);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("no deal window is allowed", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The game greys a window out once too little of it is left to travel to.
    /// The automation is told which windows the game is still offering and
    /// never picks one it is not, so a human and the automation cannot disagree
    /// about what was on offer.
    /// </summary>
    [Fact]
    public void A_window_the_game_is_not_offering_is_passed_over()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(DealWindow.Morning, DealWindow.Night);

        ScheduleDecision decision = rotation.Choose(
            allowed, new[] { DealWindow.Night }, AtMorning);

        Assert.Equal(ScheduleOutcome.Schedule, decision.Outcome);
        Assert.Equal(DealWindow.Night, decision.Window);
    }

    [Fact]
    public void When_no_allowed_window_is_on_offer_nothing_is_scheduled()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(DealWindow.Morning);

        ScheduleDecision decision = rotation.Choose(
            allowed, new[] { DealWindow.Night }, AtMorning);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("Morning", decision.Reason);
    }

    /// <summary>
    /// A window skipped because the game was not offering it must not cost the
    /// rotation its turn, or one late-afternoon deal would push every later
    /// deal into the same window.
    /// </summary>
    [Fact]
    public void A_window_that_is_not_on_offer_falls_through_to_the_next_allowed_one()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(DealWindow.Morning, DealWindow.Night);

        // Morning is not on offer, so Night takes this deal.
        Assert.Equal(
            DealWindow.Night,
            rotation.Choose(allowed, new[] { DealWindow.Night }, AtMorning).Window);

        // Morning is on offer again and still is not the answer, because the
        // clock is in it. Falling through once does not move anything
        // permanently either: there is nothing to move.
        Assert.Equal(DealWindow.Night, rotation.Choose(allowed, AnyWindow, AtMorning).Window);
        Assert.Equal(DealWindow.Night, rotation.Choose(allowed, AnyWindow, AtMorning).Window);
    }

    /// <summary>
    /// Turning a window off between two deals takes effect at once, and the
    /// deals move to the next window still allowed.
    /// </summary>
    [Fact]
    public void Turning_a_window_off_mid_session_moves_deals_to_the_next_allowed_one()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night);

        // The clock is in Morning, so Afternoon is the answer before the change
        // as well: what this checks is that turning Afternoon off moves it on
        // at once.
        Assert.Equal(DealWindow.Afternoon, rotation.Choose(allowed, AnyWindow, AtMorning).Window);

        allowed.Allow(DealWindow.Afternoon, false);

        Assert.Equal(DealWindow.Night, rotation.Choose(allowed, AnyWindow, AtMorning).Window);
        Assert.Equal(DealWindow.Night, rotation.Choose(allowed, AnyWindow, AtMorning).Window);
    }

    /// <summary>
    /// Claiming a contract is an act, not a question, so the caller must be
    /// able to find out whether a window exists before it claims one. A claim
    /// spent on a deal that then had nowhere to go would lock that offer out
    /// for the rest of its life.
    /// </summary>
    [Fact]
    public void Looking_ahead_gives_the_same_answer_as_choosing()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(DealWindow.Morning, DealWindow.Night);

        ScheduleDecision looked = rotation.Peek(allowed, AnyWindow, DealWindow.Afternoon);
        ScheduleDecision chosen = rotation.Choose(allowed, AnyWindow, DealWindow.Afternoon);

        Assert.Equal(looked.Outcome, chosen.Outcome);
        Assert.Equal(looked.Window, chosen.Window);
        Assert.Equal(looked.Reason, chosen.Reason);
    }

    [Fact]
    public void Looking_ahead_twice_does_not_move_the_rotation_on()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(DealWindow.Morning, DealWindow.Night);

        rotation.Peek(allowed, AnyWindow, AtMorning);
        rotation.Peek(allowed, AnyWindow, AtMorning);
        rotation.Peek(allowed, AnyWindow, AtMorning);

        Assert.Equal(DealWindow.Night, rotation.Choose(allowed, AnyWindow, AtMorning).Window);
    }

    [Fact]
    public void Looking_ahead_at_an_empty_set_reports_the_same_reason_as_choosing()
    {
        var rotation = new DealWindowRotation();

        Assert.Equal(
            rotation.Choose(new DealWindowSet(), AnyWindow, AtMorning).Reason,
            rotation.Peek(new DealWindowSet(), AnyWindow, AtMorning).Reason);
    }

    [Fact]
    public void A_scheduled_decision_names_the_window_it_chose()
    {
        var rotation = new DealWindowRotation();

        ScheduleDecision decision = rotation.Choose(
            Allowing(DealWindow.LateNight), AnyWindow, AtMorning);

        Assert.Contains("Late Night", decision.Reason);
    }

    // ---------------------------------------------------------------- ticket 48

    /// <summary>
    /// Ticket 48, the defect as the owner met it: <i>"Obwohl ich alle vier
    /// Tageszeiten eingestellt habe, wird der nächste Deal dann doch erst
    /// morgens gemacht."</i> His own
    /// <c>UserData/Dealcraft/decisions.jsonl</c> has it three times —
    /// <c>anna_chesterfield#32:1850</c>, an offer made at 18:50, scheduled into
    /// Morning with the reason "Morning is the next window you allow". The walk
    /// began at index 0, so Morning won whatever the clock said.
    /// </summary>
    [Fact]
    public void An_evening_offer_with_all_four_allowed_goes_into_Night_not_next_morning()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(
            DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight);

        ScheduleDecision decision = rotation.Choose(allowed, AnyWindow, DealWindow.Night);

        // Late Night rather than Night: the clock is in Night, and the current
        // window is skipped. What ticket 48 was about still holds — it is not
        // next morning.
        Assert.Equal(ScheduleOutcome.Schedule, decision.Outcome);
        Assert.Equal(DealWindow.LateNight, decision.Window);
    }

    /// <summary>
    /// Ticket 48's second acceptance criterion. Only Morning ticked and the
    /// clock in the evening: Morning is still the answer, it is still correct,
    /// and the reason has to say which Morning it means.
    /// </summary>
    [Fact]
    public void An_evening_offer_with_only_Morning_allowed_says_the_Morning_is_tomorrows()
    {
        var rotation = new DealWindowRotation();

        ScheduleDecision decision = rotation.Choose(
            Allowing(DealWindow.Morning), AnyWindow, DealWindow.Night);

        Assert.Equal(DealWindow.Morning, decision.Window);
        Assert.Equal("Morning is the next window you allow, and it is tomorrow's", decision.Reason);
    }

    /// <summary>
    /// The current window is skipped, from every one of the four.
    /// </summary>
    /// <remarks>
    /// This asserted the opposite, and cited the game's own gate for it:
    /// <c>DealWindowSelector.IsWindowValid</c> (RVA 0x9FA3D0) withholds a window
    /// with too little of it left, so a window still on offer was taken to be
    /// one there was still time for. The owner met the result on 2026-09-19 —
    /// a deal taken at 14:25 went into that same afternoon — and decided
    /// against it: <i>"Ich dachte immer in die nächste. Und das ist auch ein
    /// Bug."</i> The old comment also claimed he had said the opposite, which
    /// is why this one quotes him.
    /// </remarks>
    [Theory]
    [InlineData(DealWindow.Morning)]
    [InlineData(DealWindow.Afternoon)]
    [InlineData(DealWindow.Night)]
    [InlineData(DealWindow.LateNight)]
    public void With_all_four_allowed_the_window_the_game_is_in_is_not_the_one(DealWindow now)
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(
            DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight);

        ScheduleDecision decision = rotation.Choose(allowed, AnyWindow, now);

        Assert.Equal(ScheduleOutcome.Schedule, decision.Outcome);
        Assert.NotEqual(now, decision.Window);
        Assert.DoesNotContain("the window the game is in now", decision.Reason);
    }

    /// <summary>
    /// A host who allows only the window the clock is already in gets nothing
    /// scheduled until that window ends.
    /// </summary>
    /// <remarks>
    /// This briefly asserted that the walk came back round and scheduled into
    /// tomorrow's occurrence of the same window. It cannot: the accept hands the
    /// game an <c>EDealWindow</c> and no date, so while the clock is inside the
    /// window the game means the one running out, and "tomorrow's" would be a
    /// sentence in the log rather than a fact about the deal. The owner put it
    /// in an example — only Morning ticked and the clock in Morning — and
    /// answered it himself: <i>"dann musst du warten bis es Mittag ist und dann
    /// für den nächsten Morgen einplanen."</i>
    /// </remarks>
    [Theory]
    [InlineData(DealWindow.Morning)]
    [InlineData(DealWindow.Afternoon)]
    [InlineData(DealWindow.Night)]
    [InlineData(DealWindow.LateNight)]
    public void The_only_allowed_window_being_the_current_one_waits(DealWindow now)
    {
        var rotation = new DealWindowRotation();

        ScheduleDecision decision = rotation.Choose(Allowing(now), AnyWindow, now);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("the clock is in it", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("running out", decision.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// And once the clock has moved on, that same window is scheduled into —
    /// the next occurrence of it, which is what the wait was for. The owner's
    /// own example: only Morning ticked, wait through Morning, and at midday
    /// take the next one.
    /// </summary>
    [Fact]
    public void Once_the_clock_leaves_it_the_only_allowed_window_is_scheduled()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet onlyMorning = Allowing(DealWindow.Morning);

        Assert.Equal(
            ScheduleOutcome.Skip,
            rotation.Choose(onlyMorning, AnyWindow, DealWindow.Morning).Outcome);

        ScheduleDecision atMidday = rotation.Choose(onlyMorning, AnyWindow, DealWindow.Afternoon);

        Assert.Equal(ScheduleOutcome.Schedule, atMidday.Outcome);
        Assert.Equal(DealWindow.Morning, atMidday.Window);
        Assert.Equal(
            "Morning is the next window you allow, and it is tomorrow's", atMidday.Reason);
    }

    /// <summary>
    /// The walk from each of the four starting windows, over the whole wrap:
    /// the current window is not on offer, so the answer is the one after it,
    /// and after Late Night that is Morning again.
    /// </summary>
    [Theory]
    [InlineData(DealWindow.Morning, DealWindow.Afternoon)]
    [InlineData(DealWindow.Afternoon, DealWindow.Night)]
    [InlineData(DealWindow.Night, DealWindow.LateNight)]
    [InlineData(DealWindow.LateNight, DealWindow.Morning)]
    public void The_walk_wraps_past_the_end_of_the_day(DealWindow now, DealWindow expected)
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(
            DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight);

        // Everything the game is offering except the window it is in, which is
        // what a selector says in the last two hours of a window.
        DealWindow[] onOffer = DealWindowSet.All.Where(window => window != now).ToArray();

        Assert.Equal(expected, rotation.Choose(allowed, onOffer, now).Window);
    }

    /// <summary>
    /// The whole wrap from one starting window: with exactly one window ticked
    /// at a time, an offer at Night finds each of the four, and it is always
    /// the soonest occurrence of it.
    /// </summary>
    [Theory]
    [InlineData(DealWindow.LateNight, "Late Night is the next window you allow, and it is tomorrow's")]
    [InlineData(DealWindow.Morning, "Morning is the next window you allow, and it is tomorrow's")]
    [InlineData(DealWindow.Afternoon, "Afternoon is the next window you allow, and it is tomorrow's")]
    public void From_Night_every_single_allowed_window_is_found_and_dated(
        DealWindow only, string reason)
    {
        var rotation = new DealWindowRotation();

        ScheduleDecision decision = rotation.Choose(Allowing(only), AnyWindow, DealWindow.Night);

        Assert.Equal(only, decision.Window);
        Assert.Equal(reason, decision.Reason);
    }

    /// <summary>
    /// The mirror of the case above, and the one the index wrap would get
    /// wrong: the day rolls over at midnight, not at Late Night's end. An offer
    /// at 2 a.m. that goes into Morning goes into <b>today's</b> Morning, four
    /// hours later, and the reason must not call it tomorrow's.
    /// <c>TimeManager.RpcLogic___PassMinute_Client</c> (RVA 0xA92EA0) is what
    /// settles it: only a clock of 2359 increments ElapsedDays.
    /// </summary>
    [Theory]
    [InlineData(DealWindow.Morning, "Morning is the next window you allow today")]
    [InlineData(DealWindow.Afternoon, "Afternoon is the next window you allow today")]
    [InlineData(DealWindow.Night, "Night is the next window you allow today")]
    public void From_Late_Night_nothing_is_tomorrows_because_midnight_has_already_passed(
        DealWindow only, string reason)
    {
        var rotation = new DealWindowRotation();

        ScheduleDecision decision = rotation.Choose(
            Allowing(only), AnyWindow, DealWindow.LateNight);

        Assert.Equal(only, decision.Window);
        Assert.Equal(reason, decision.Reason);
    }

    /// <summary>
    /// From Morning and from Afternoon, the date changes exactly at Late Night.
    /// </summary>
    [Theory]
    [InlineData(DealWindow.Morning, DealWindow.Afternoon, "Afternoon is the next window you allow today")]
    [InlineData(DealWindow.Morning, DealWindow.Night, "Night is the next window you allow today")]
    [InlineData(DealWindow.Morning, DealWindow.LateNight, "Late Night is the next window you allow, and it is tomorrow's")]
    [InlineData(DealWindow.Afternoon, DealWindow.Night, "Night is the next window you allow today")]
    [InlineData(DealWindow.Afternoon, DealWindow.LateNight, "Late Night is the next window you allow, and it is tomorrow's")]
    [InlineData(DealWindow.Afternoon, DealWindow.Morning, "Morning is the next window you allow, and it is tomorrow's")]
    public void The_date_in_the_reason_turns_over_at_Late_Night(
        DealWindow now, DealWindow only, string reason)
    {
        var rotation = new DealWindowRotation();

        ScheduleDecision decision = rotation.Choose(Allowing(only), AnyWindow, now);

        Assert.Equal(only, decision.Window);
        Assert.Equal(reason, decision.Reason);
    }

    /// <summary>
    /// The three answered offers in the owner's own session, replayed from
    /// their contract keys. <c>elizabeth_homley#32:1020</c> is an offer at
    /// 10:20, which is Morning with 100 minutes of it left — under the game's
    /// 120-minute cutoff, so Morning was not on offer and Afternoon was right
    /// even before this fix. <c>peggy_myers#32:1730</c> and
    /// <c>anna_chesterfield#32:1850</c> are the two that went wrong.
    /// <para>
    /// The third row used to expect Night for the 18:50 offer, because the
    /// current window was taken while the game still offered it. The owner has
    /// since ruled the current window out altogether, so the same offer now
    /// waits for Late Night. Nothing about the other two changes: their current
    /// window was not on offer either way.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(DealWindow.Morning, false, DealWindow.Afternoon)]   // 10:20, Morning nearly over
    [InlineData(DealWindow.Afternoon, false, DealWindow.Night)]     // 17:30, Afternoon nearly over
    [InlineData(DealWindow.Night, true, DealWindow.LateNight)]      // 18:50, Night wide open but current
    public void The_owners_session_replayed_from_its_contract_keys(
        DealWindow now, bool currentWindowStillOnOffer, DealWindow expected)
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(
            DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight);

        DealWindow[] onOffer = currentWindowStillOnOffer
            ? DealWindowSet.All.ToArray()
            : DealWindowSet.All.Where(window => window != now).ToArray();

        Assert.Equal(expected, rotation.Choose(allowed, onOffer, now).Window);
    }

    /// <summary>
    /// A clock the game could not be asked about is a real answer. The walk
    /// falls back to the top of the day — what it did before ticket 48 — and
    /// the reason stops claiming to know what "next" is, because it does not.
    /// </summary>
    [Fact]
    public void Without_a_clock_the_walk_starts_at_the_top_of_the_day_and_says_so()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(
            DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight);

        ScheduleDecision decision = rotation.Choose(allowed, AnyWindow, now: null);

        Assert.Equal(DealWindow.Morning, decision.Window);
        Assert.Equal(
            "Morning is the first window you allow, and the game's clock could not be read "
                + "to count from now",
            decision.Reason);
    }

    /// <summary>
    /// The reason is true of the window that was picked, in every branch.
    /// </summary>
    /// <remarks>
    /// It used to check that a current window was never called "next", which
    /// was the right check while the current window could be picked. It cannot
    /// be any more, so what is left to hold is the other half of the same rule:
    /// the window named in the reason is the window that was chosen, and no
    /// reason claims the clock is standing in it.
    /// </remarks>
    [Fact]
    public void Every_reason_names_the_window_that_was_actually_chosen()
    {
        var rotation = new DealWindowRotation();
        DealWindowSet allowed = Allowing(
            DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight);

        foreach (DealWindow now in DealWindowSet.All)
        {
            ScheduleDecision decision = rotation.Choose(allowed, AnyWindow, now);

            Assert.NotEqual(now, decision.Window);
            Assert.StartsWith(DealWindowName.Of(decision.Window), decision.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain("the window the game is in now", decision.Reason, StringComparison.Ordinal);
        }
    }
}
