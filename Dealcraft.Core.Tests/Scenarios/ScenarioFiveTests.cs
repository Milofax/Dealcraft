using System;
using System.Collections.Generic;
using Xunit;

namespace Dealcraft.Core.Tests.Scenarios;

/// <summary>
/// Scenario 5: a request at 11:40pm, from a player who does not deal at night.
/// </summary>
[Drives(5, "A request at night, when I do not deal at night")]
public class ScenarioFiveTests
{
    private const int Scenario = 5;

    private static readonly string TheirContract =
        ContractKey.ForOffer("jessi_waters", elapsedDays: 12, timeOfDay: 2340);

    private static PendingOffer SomeoneTexts() => new(
        TheirContract, "Jessi Waters", hasOfferedContract: true, alreadyOnADeal: false);

    /// <summary>Morning and Afternoon ticked; the two night windows are not.</summary>
    private static DealWindowSet MorningAndAfternoon() =>
        AnEveningOfPlay.Allowing(DealWindow.Morning, DealWindow.Afternoon);

    /// <summary>
    /// 11:40pm, which is the Night window (6:00 PM to 12:00 AM). Since ticket 48
    /// the walk over the windows starts where the clock is, so the scenario's
    /// own time of day is an input to it.
    /// </summary>
    private const DealWindow ElevenForty = DealWindow.Night;

    /// <summary>
    /// Step 2: it does not schedule into the night. At 11:40pm the game is
    /// offering the night windows, and neither is one the player allows.
    /// </summary>
    [Fact]
    public void It_does_not_schedule_the_deal_into_the_night()
    {
        TheScenarioFile.Says(Scenario, "Dealcraft does **not** schedule it into the night.");

        var onOffer = new[] { DealWindow.Night, DealWindow.LateNight };

        ScheduleDecision decision = DealScheduler.ChooseWindow(
            new ContractClaimRegistry().Claim(TheirContract),
            MorningAndAfternoon(),
            onOffer,
            ElevenForty,
            new DealWindowRotation());

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.NotEqual(DealWindow.Night, decision.Window);
    }

    /// <summary>
    /// Step 4: the sentence, verbatim. It is the reason the scheduler gives
    /// while it waits, and it says which windows are allowed as well.
    /// </summary>
    [Fact]
    public void It_says_the_game_is_offering_none_of_the_allowed_windows_right_now()
    {
        string sentence = TheScenarioFile.Says(
            Scenario, "`the game is offering none of the allowed windows right now`");

        ScheduleDecision decision = new DealWindowRotation().Peek(
            MorningAndAfternoon(), new[] { DealWindow.Night, DealWindow.LateNight }, ElevenForty);

        Assert.StartsWith(
            sentence.Trim('`'), decision.Reason, StringComparison.Ordinal);

        Assert.Equal(
            "the game is offering none of the allowed windows right now "
            + "(allowed: Morning, Afternoon)",
            decision.Reason);
    }

    /// <summary>
    /// Step 3: it waits, and takes the next window the player allows that the
    /// game offers — which in the morning is the morning.
    /// </summary>
    [Fact]
    public void It_waits_and_takes_the_next_allowed_window_the_game_offers()
    {
        TheScenarioFile.Says(Scenario, "It waits for a window I allowed and takes the next one the game offers.");

        var rotation = new DealWindowRotation();
        DealWindowSet allowed = MorningAndAfternoon();

        // Nothing has changed but the clock. Peeking while the night windows
        // are up costs nothing and claims nothing, which is what lets it wait.
        Assert.Equal(
            ScheduleOutcome.Skip,
            rotation.Peek(
                allowed, new[] { DealWindow.Night, DealWindow.LateNight }, ElevenForty).Outcome);

        // The clock is in Morning and Afternoon is the next window allowed, so
        // that is the one. "The next one the game offers" is the scenario's own
        // wording, and it is what this now does: the window the day is already
        // in is no longer a candidate.
        ScheduleDecision afternoon = DealScheduler.ChooseWindow(
            new ContractClaimRegistry().Claim(TheirContract),
            allowed,
            new[] { DealWindow.Morning, DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight },
            DealWindow.Morning,
            rotation);

        Assert.Equal(ScheduleOutcome.Schedule, afternoon.Outcome);
        Assert.Equal(DealWindow.Afternoon, afternoon.Window);
        Assert.Equal("Afternoon is the next window you allow today", afternoon.Reason);
    }

    /// <summary>
    /// "With all four windows off": nothing is scheduled at all, and it is a
    /// sentence rather than silence.
    /// </summary>
    /// <remarks>
    /// The sentence is not the scenario's, and there is only one of them. The
    /// scheduler's is asserted here, so the divergence is a comparison rather
    /// than an assertion of absence.
    /// <para>
    /// The form had a second, <c>DealWindowCatalog.NothingAllowed</c> — except
    /// that no production code read it and the block draws four toggles and
    /// nothing else. It is deleted; the page saying so is ticket 45.
    /// </para>
    /// </remarks>
    [Fact]
    public void With_all_four_off_nothing_is_scheduled_and_it_is_said_out_loud()
    {
        TheScenarioFile.Says(Scenario, "The block says `no window is allowed` and nothing is scheduled at all. Not\nsilence — a sentence.");

        var none = new DealWindowSet();
        Assert.True(none.AllowsNothing);

        // Nothing is scheduled: the four windows are the switch, so all four off
        // is scheduling switched off.
        ScheduleDecision considered = DealScheduler.Consider(
            SomeoneTexts(), ServerAuthority.Held, automationOn: !none.AllowsNothing);
        Assert.Equal(ScheduleOutcome.Skip, considered.Outcome);
        Assert.Equal("deal scheduling is switched off", considered.Reason);

        // And a sentence, not silence — but not this sentence.
        ScheduleDecision scanned = new DealWindowRotation().Peek(none, DealWindowSet.All, ElevenForty);
        Assert.Equal(
            "no deal window is allowed, so nothing is auto-scheduled; "
            + "allow at least one window to turn scheduling back on",
            scanned.Reason);

        Assert.NotEqual("no window is allowed", scanned.Reason);
    }

    /// <summary>
    /// <b>Divergence.</b> The scenario puts both sentences on an "Accept-offers
    /// block". The block is called ACCEPTED SCHEDULE, and like every other block
    /// it draws no reading — four window toggles and nothing else.
    /// </summary>
    [Fact]
    public void The_block_is_four_toggles_and_carries_neither_sentence()
    {
        TheScenarioFile.Says(Scenario, "the Accept-offers block says");

        AutomationForm page = AFullyConfiguredPage.Of(
            new AdvisorSettings(), MorningAndAfternoon());

        Assert.DoesNotContain(page.Blocks, block => block.Title == "Accept-offers");

        AutomationBlock schedule = Assert.Single(
            page.Blocks, block => block.Title == "ACCEPTED SCHEDULE");

        Assert.Equal(4, schedule.Rows.Count);
        Assert.All(schedule.Rows, row => Assert.Equal(FormRowKind.Toggle, row.Kind));

        Assert.DoesNotContain(schedule.Rows, row =>
            row.Text.Contains("allowed window", StringComparison.OrdinalIgnoreCase)
            || row.Value.Contains("allowed window", StringComparison.OrdinalIgnoreCase)
            || row.Value.Contains("no window is allowed", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The record is where the waiting is visible, and it carries the reason and
    /// no window.
    /// </summary>
    [Fact]
    public void The_record_says_it_is_waiting_and_names_no_window()
    {
        var lines = new List<string>();
        var recorder = new DebugRecorder(line =>
        {
            lines.Add(line);
            return true;
        });

        ScheduleDecision waiting = new DealWindowRotation().Peek(
            MorningAndAfternoon(), new[] { DealWindow.Night, DealWindow.LateNight }, ElevenForty);

        recorder.Record(DebugRow.Scheduling(
            waiting.Outcome.ToString(), acted: false, waiting.Reason,
            "Jessi Waters", TheirContract, window: null));

        string row = Assert.Single(lines);

        Assert.Contains("\"outcome\":\"Skip\"", row, StringComparison.Ordinal);
        Assert.Contains("\"window\":null", row, StringComparison.Ordinal);
        Assert.Contains(
            "\"reason\":\"the game is offering none of the allowed windows right now",
            row,
            StringComparison.Ordinal);
    }
}
