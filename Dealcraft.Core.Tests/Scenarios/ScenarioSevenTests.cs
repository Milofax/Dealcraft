using System;
using System.Collections.Generic;
using Xunit;

namespace Dealcraft.Core.Tests.Scenarios;

/// <summary>
/// Scenario 7: a fresh install with nothing switched on. The app is the only
/// thing that changed about the game.
/// </summary>
[Drives(7, "Everything off")]
public class ScenarioSevenTests
{
    private const int Scenario = 7;

    /// <summary>What a fresh install holds, from the type the file fills.</summary>
    private static AdvisorSettings AFreshInstall() => new();

    /// <summary>
    /// It sends nothing: the negotiation gate refuses before it looks at
    /// anything else, so a host who never turned it on gets one quiet line
    /// rather than a commentary.
    /// </summary>
    [Fact]
    public void It_sends_nothing()
    {
        TheScenarioFile.Says(Scenario, "Dealcraft sends nothing");

        AdvisorSettings settings = AFreshInstall();
        Assert.False(settings.AutoCounterOffer);

        var offer = new PendingOffer(
            ContractKey.ForOffer("beth_penn", 1, 1015), "Beth Penn",
            hasOfferedContract: true, alreadyOnADeal: false);

        CounterofferDecision decision = CounterofferGate.Consider(
            offer, ServerAuthority.Held, settings.AutoCounterOffer);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Equal("automatic counter-offers are switched off", decision.Reason);
    }

    /// <summary>
    /// It schedules nothing: no window ships ticked, and four unticked windows
    /// are how scheduling is off — there is no switch above them.
    /// </summary>
    [Fact]
    public void It_schedules_nothing()
    {
        TheScenarioFile.Says(Scenario, "schedules\nnothing");

        var windows = new DealWindowSet();
        Assert.True(windows.AllowsNothing);

        var offer = new PendingOffer(
            ContractKey.ForOffer("beth_penn", 1, 1015), "Beth Penn",
            hasOfferedContract: true, alreadyOnADeal: false);

        ScheduleDecision decision = DealScheduler.Consider(
            offer, ServerAuthority.Held, automationOn: !windows.AllowsNothing);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Equal("deal scheduling is switched off", decision.Reason);
    }

    /// <summary>It hands over nothing.</summary>
    [Fact]
    public void It_hands_over_nothing()
    {
        TheScenarioFile.Says(Scenario, "hands over nothing");

        AdvisorSettings settings = AFreshInstall();
        Assert.False(settings.AutoHandover);

        HandoverDecision decision = HandoverGate.Decide(new HandoverSituation(
            CustomerName: "Beth Penn",
            ContractKey: ContractKey.ForOffer("beth_penn", 1, 1015),
            CanReachTheServer: true,
            AutomationEnabled: settings.AutoHandover,
            IsReadyForHandover: true,
            IsHandoverChoiceValid: true,
            HandoverChoiceInvalidReason: string.Empty,
            Place: settings.Place,
            TheyOpenedTheDialogue: true));

        Assert.Equal(HandoverAction.Abstain, decision.Action);
        Assert.Equal("automatic handover is switched off", decision.Reason);
    }

    /// <summary>And it writes no price anywhere.</summary>
    [Fact]
    public void It_writes_no_price_anywhere()
    {
        TheScenarioFile.Says(Scenario, "writes no price anywhere");

        var roster = new[] { new ProductCandidate("Beth Penn", 10, 520f, 0f) };

        ListedPriceDecision decision = ListedPriceDecision.For(
            maintaining: false, "OG Kush", listedPrice: 52f,
            PricingRecommendation.Best(roster));

        Assert.Equal(ListedPriceOutcome.LeaveAlone, decision.Outcome);
        Assert.Equal(0f, decision.Price);
        Assert.Equal("you set your own prices", decision.Reason);
    }

    /// <summary>
    /// And every switch on the fresh page is at the game's own behaviour, which
    /// is what "the app is the only thing that changed" has to mean.
    /// </summary>
    [Fact]
    public void Every_question_on_a_fresh_page_is_answered_with_the_games_own_behaviour()
    {
        TheScenarioFile.Says(Scenario, "The app is the only thing that changed about my game.");

        AutomationForm page = AFullyConfiguredPage.Of(AFreshInstall(), new DealWindowSet());

        Assert.Equal(AutomationForm.HostMode, page.Mode);

        foreach (AutomationBlock block in page.Blocks)
        {
            foreach (FormRow row in block.Rows)
            {
                if (row.Kind == FormRowKind.Toggle)
                {
                    Assert.False(row.Chosen);
                    continue;
                }

                if (row.Kind == FormRowKind.Choice && row.Chosen)
                {
                    Assert.Equal(TwoAnswers.TheGamesOwnBehaviour, row.Text);
                }
            }
        }
    }

    /// <summary>
    /// <b>Divergence.</b> "The Automation tab is four lines and every block says
    /// `off`." It is four <em>blocks</em> and twelve rows, and no block says
    /// <c>off</c>: a question off is its "Manual (game's default)" answer
    /// ticked, and the schedule block's four windows are four unticked toggles.
    /// </summary>
    /// <remarks>
    /// The count is asserted so that a page growing a row is a change somebody
    /// has to look at. What the page draws with everything off is exactly: two
    /// answers for the listed price, two for the negotiation, four windows, and
    /// the handover's three answers with the line under the third. The chance
    /// floor and the grade question are not drawn, because the page does not
    /// draw a setting that does not currently apply.
    /// </remarks>
    [Fact]
    public void The_tab_is_four_blocks_of_twelve_rows_and_none_of_them_says_off()
    {
        TheScenarioFile.Says(Scenario, "The Automation tab is\nfour lines and every block says `off`.");

        AutomationForm page = AFullyConfiguredPage.Of(AFreshInstall(), new DealWindowSet());

        Assert.Equal(4, page.Blocks.Count);
        Assert.Equal(
            new[] { "LISTED PRICE", "PRICE NEGOTIATION", "ACCEPTED SCHEDULE", "HANDOVER" },
            Titles(page));

        var rows = 0;
        foreach (AutomationBlock block in page.Blocks)
        {
            rows += block.Rows.Count;

            Assert.DoesNotContain(block.Rows, row =>
                string.Equals(row.Text, "off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.Value, "off", StringComparison.OrdinalIgnoreCase));
        }

        Assert.Equal(12, rows);
    }

    /// <summary>
    /// A fresh install writes no record rows either, because none of the four
    /// verdicts above is news worth a line — the gates say so themselves by
    /// refusing first and quietly.
    /// </summary>
    [Fact]
    public void Nothing_is_recorded_twice_for_a_switch_that_stays_off()
    {
        var lines = new List<string>();
        var recorder = new DebugRecorder(line =>
        {
            lines.Add(line);
            return true;
        });

        string key = ContractKey.ForOffer("beth_penn", 1, 1015);

        CounterofferDecision off = CounterofferGate.Consider(
            new PendingOffer(key, "Beth Penn", hasOfferedContract: true, alreadyOnADeal: false),
            ServerAuthority.Held,
            automationOn: false);

        // Two sweeps, five seconds apart, saying the same thing.
        for (var sweep = 0; sweep < 2; sweep++)
        {
            recorder.Record(DebugRow.Negotiation(
                off.Outcome.ToString(), acted: false, off.Reason, "Beth Penn", key));
        }

        string row = Assert.Single(lines);
        Assert.Contains("automatic counter-offers are switched off", row, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> Titles(AutomationForm page)
    {
        var titles = new List<string>(page.Blocks.Count);
        foreach (AutomationBlock block in page.Blocks)
        {
            titles.Add(block.Title);
        }

        return titles;
    }
}
