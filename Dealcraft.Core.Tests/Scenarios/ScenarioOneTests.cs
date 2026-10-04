using static Dealcraft.Core.Tests.Floors;
using System;
using System.Collections.Generic;
using Xunit;

namespace Dealcraft.Core.Tests.Scenarios;

/// <summary>
/// Scenario 1, driven from the text message to the money.
/// </summary>
/// <remarks>
/// <para>
/// Beth's offer goes through the gate, the claim, the price curve at every
/// confidence, the chance floor, the window, the packing, the handover gate and
/// the ledger — the shipped code at every step, called and never copied.
/// </para>
/// <para>
/// <b>Two of the scenario's lines do not survive the reading and one of them is
/// the headline.</b> They are asserted as divergences rather than rewritten;
/// which line should say what is in the report.
/// </para>
/// </remarks>
[Drives(1, "The best price, from the text message to the money")]
public class ScenarioOneTests
{
    private const int Scenario = 1;

    /// <summary>OG Kush is listed at $52, and the listed price is the floor.</summary>
    private const float ListedPrice = 52f;

    /// <summary>What Beth is offering: 8 units for $640.</summary>
    private const int Order = 8;

    private const float Offered = 640f;

    /// <summary>What the scenario says she would still take, and how likely.</summary>
    private const float Counter = 760f;

    private const float NinetyPerCent = 0.9f;

    /// <summary>10:15 on day twelve, which is where the key comes from.</summary>
    private static readonly string BethsContract =
        ContractKey.ForOffer("beth_penn", elapsedDays: 12, timeOfDay: 1015);

    private static PendingOffer BethTexts() => new(
        BethsContract, "Beth Penn", hasOfferedContract: true, alreadyOnADeal: false);

    /// <summary>
    /// A customer for whom the scenario's sentence is true: $760 is the most she
    /// would take at ninety percent, and above it she stops quickly.
    /// </summary>
    private static Func<int, float, float> BethWithACliff() =>
        AnEveningOfPlay.ACustomerWhoFallsAway(
            certainUpTo: Offered, anchorTotal: Counter, anchorChance: NinetyPerCent, zeroAt: 800f);

    /// <summary>
    /// The same sentence is true of this one too, and she is not a cliff: her
    /// chance falls away gently, so there is more money above $760 than there is
    /// at it.
    /// </summary>
    private static Func<int, float, float> BethWithASlope() =>
        AnEveningOfPlay.ACustomerWhoFallsAway(
            certainUpTo: Offered, anchorTotal: Counter, anchorChance: NinetyPerCent, zeroAt: 1840f);

    /// <summary>
    /// Steps 1 to 3: the text arrives, the curve is drawn, the counter goes out
    /// once and Beth accepts.
    /// </summary>
    [Fact]
    public void The_counter_is_worked_out_without_contacting_her_and_sent_once()
    {
        TheScenarioFile.Says(Scenario, "she wants **8 OG Kush for $640**");
        TheScenarioFile.Says(Scenario, "she would still say yes to\n   **$760** with a 90% chance. It sends that, once.");
        TheScenarioFile.Says(Scenario, "Dealcraft works out, without contacting her");

        var claims = new ContractClaimRegistry();

        CounterofferDecision eligible = CounterofferGate.Consider(
            BethTexts(), ServerAuthority.Held, automationOn: true);
        Assert.Equal(CounterofferOutcome.Claim, eligible.Outcome);

        // "without contacting her": every probe is a local read of the chance,
        // and the one thing spent is the counter-offer itself.
        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, ListedPrice, NoChanceFloor, BethWithACliff());

        Assert.Equal(Order, found.Planned.Quantity);
        Assert.Equal(Counter, found.Planned.TotalPrice);
        Assert.Equal(NinetyPerCent, found.Chance, 3);

        CounterofferDecision send = CounterofferGate.Send(
            claims.Claim(BethsContract),
            found.Planned,
            found.Chance,
            Offered,
            floor: NinetyPerCent);

        Assert.Equal(CounterofferOutcome.Send, send.Outcome);
        Assert.Equal(Counter, send.TotalPrice);

        // Once. The claim is what makes it once, and the next sweep meets it.
        CounterofferDecision again = CounterofferGate.Send(
            claims.Claim(BethsContract), found.Planned, found.Chance, Offered, NinetyPerCent);

        Assert.Equal(CounterofferOutcome.Skip, again.Outcome);
        Assert.Contains("already claimed", again.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>And it only happens for one of the two Beths.</b> The scenario names a
    /// single point on her curve — $760 at ninety percent — and the mod's search
    /// ranks every confidence by <c>chance × price</c> over the whole of it. For
    /// a customer who falls away gently, $880 at eighty percent is worth more,
    /// the search picks that, and the ninety percent floor then refuses it: the
    /// floor is applied to the winner and never reaches back for another rung
    /// (<see cref="ChanceFloor"/>). Nothing is sent and the $640 stands.
    /// </summary>
    /// <remarks>
    /// This is the finding, not a defect: <c>ConfidenceSearch</c> and
    /// <c>ChanceFloor</c> both state the behaviour on purpose, and
    /// <c>CounterofferGate.Send</c> carries the arithmetic showing why searching
    /// at the player's own confidence loses money. The scenario's step 2 asserts
    /// an outcome its own premise does not determine.
    /// </remarks>
    [Fact]
    public void The_same_sentence_about_Beth_can_leave_nothing_sent_at_all()
    {
        Func<int, float, float> slope = BethWithASlope();

        // The scenario's premise holds for her too, to the penny.
        Assert.Equal(NinetyPerCent, slope(Order, Counter), 3);

        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, ListedPrice, NoChanceFloor, slope);

        // The search does not find $760. It finds more money at less confidence.
        Assert.True(found.Planned.TotalPrice > Counter);
        Assert.True(found.Confidence < NinetyPerCent);

        CounterofferDecision send = CounterofferGate.Send(
            new ContractClaimRegistry().Claim(BethsContract),
            found.Planned,
            found.Chance,
            Offered,
            floor: NinetyPerCent);

        Assert.Equal(CounterofferOutcome.Skip, send.Outcome);
        Assert.Contains("so the offer on the table stands", send.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Step 4: the deal is set for the afternoon, because that is the next
    /// window the player allows that the game is offering. Not a turn.
    /// </summary>
    [Fact]
    public void The_deal_is_set_for_the_next_allowed_window_the_game_is_offering()
    {
        TheScenarioFile.Says(Scenario, "When: Morning ✅ Afternoon ✅ Night ☐ Late Night ☐");
        TheScenarioFile.Says(
            Scenario,
            "The deal is set for the **afternoon** — the next window I allow that the game\n   is offering. Not \"the one whose turn it was\": there is no turn.");

        DealWindowSet allowed = AnEveningOfPlay.Allowing(DealWindow.Morning, DealWindow.Afternoon);

        // 10:15 in the morning: under two hours of the morning window are left,
        // so the game has greyed it out and is offering the three that have not
        // started.
        var onOffer = new[] { DealWindow.Afternoon, DealWindow.Night, DealWindow.LateNight };

        // ...and the clock is inside Morning, which is where the walk begins.
        const DealWindow now = DealWindow.Morning;

        ScheduleDecision eligible = DealScheduler.Consider(
            BethTexts(), ServerAuthority.Held, automationOn: !allowed.AllowsNothing);
        Assert.Equal(ScheduleOutcome.Claim, eligible.Outcome);

        var rotation = new DealWindowRotation();
        ScheduleDecision chosen = DealScheduler.ChooseWindow(
            new ContractClaimRegistry().Claim(BethsContract), allowed, onOffer, now, rotation);

        Assert.Equal(ScheduleOutcome.Schedule, chosen.Outcome);
        Assert.Equal(DealWindow.Afternoon, chosen.Window);
        Assert.Equal("Afternoon is the next window you allow today", chosen.Reason);

        // "There is no turn": asking again gives the same window, not the next
        // one round. DealWindowRotation holds no state at all.
        Assert.Equal(DealWindow.Afternoon, rotation.Choose(allowed, onOffer, now).Window);
        Assert.Equal(DealWindow.Afternoon, rotation.Choose(allowed, onOffer, now).Window);
    }

    /// <summary>
    /// Steps 5 and 6: at 4:30pm, with eight packaged OG Kush in his pockets, the
    /// handover fills itself with the right eight and finishes.
    /// </summary>
    [Fact]
    public void Eight_packaged_units_leave_the_pockets_and_the_handover_is_valid()
    {
        TheScenarioFile.Says(Scenario, "I walk to her with 8 packaged OG Kush in my pockets");
        TheScenarioFile.Says(Scenario, "The handover screen opens, fills itself with the right 8, and finishes.");

        var request = new DeliveryRequest(
            "ogkush",
            RequestedQuality: QualityTier.Standard,
            RequestedQuantity: Order,
            ContractPayment: Counter,
            CustomerStandard: QualityTier.Standard);

        // Eight baggies of the grade the contract asked for. Four packages is
        // all a handover holds, so they are two jars and two baggies rather
        // than eight of anything.
        var pockets = new List<CarriedLot>
        {
            new(QualityTier.Standard, unitsPerPackage: 3, packages: 2),
            new(QualityTier.Standard, unitsPerPackage: 1, packages: 2),
        };

        GradePlan plan = GradeChoice.Plan(
            request, pockets, GradeReach.Exactly, out PackageFill fill);

        Assert.True(plan.CanDeliver);
        Assert.Equal(QualityTier.Standard, plan.ChosenQuality);
        Assert.Equal(Order, plan.ChosenUnits);

        // "the right 8": exactly the order, nothing given away.
        Assert.Equal(Order, fill.Units);
        Assert.Equal(4, fill.Packages);

        HandoverDecision decision = HandoverGate.Decide(new HandoverSituation(
            CustomerName: "Beth Penn",
            ContractKey: BethsContract,
            CanReachTheServer: true,
            AutomationEnabled: true,
            IsReadyForHandover: true,
            IsHandoverChoiceValid: true,
            HandoverChoiceInvalidReason: string.Empty,

            // Step 6 is the player talking to Beth Penn, which is what the
            // default answer asks for.
            Place: HandoverPlace.WhenITalkToThem,
            TheyOpenedTheDialogue: true));

        Assert.Equal(HandoverAction.HandOver, decision.Action);
    }

    /// <summary>
    /// Step 7: $760 lands. The delivery is exactly what was ordered, at the
    /// grade that was ordered, so the mod's own arithmetic predicts no bonus and
    /// the money is the contract's payment.
    /// </summary>
    [Fact]
    public void Seven_hundred_and_sixty_lands()
    {
        TheScenarioFile.Says(Scenario, "**$760 lands.**");

        HandoverPrediction predicted = HandoverPrediction.Of(
            payment: Counter,
            requestedUnits: Order,
            deliveredUnits: Order,
            satisfaction: 1f,
            qualityTiers: 0f,
            curfewActive: false,
            withinQuickWindow: false,
            rainy: 0f);

        Assert.True(predicted.IsComplete);
        Assert.Equal(0f, predicted.Total);

        var row = new HandoverLedgerRow
        {
            CustomerName = "Beth Penn",
            ContractKey = BethsContract,
            HandoverByPlayer = false,
            Outcome = "Finalize",
            ContractPayment = Counter,
            TotalPayment = Counter,
            DeliveredUnits = Order,
            QualityTiers = 0f,
            Prediction = predicted,
        };

        Assert.Equal(Counter, row.TotalPayment);
        Assert.Equal(0f, row.MeasuredBonusTotal);
        Assert.Contains("\"total_payment\":760", row.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>
    /// "If Beth says no": nothing else is sent, and Dealcraft does not try a
    /// second price.
    /// </summary>
    [Fact]
    public void If_she_says_no_nothing_else_is_sent()
    {
        TheScenarioFile.Says(Scenario, "Nothing else is sent. She is left alone for that offer");
        TheScenarioFile.Says(Scenario, "I\nam not asked again and Dealcraft does not try a second price.");

        // A refused counter leaves the game's own flag set, which survives a
        // reload where the claim registry does not.
        var afterARefusal = new PendingOffer(
            BethsContract, "Beth Penn",
            hasOfferedContract: true, alreadyOnADeal: false, alreadyCountered: true);

        CounterofferDecision decision = CounterofferGate.Consider(
            afterARefusal, ServerAuthority.Held, automationOn: true);

        Assert.Equal(CounterofferOutcome.Skip, decision.Outcome);
        Assert.Equal("Beth Penn's offer has already been countered once", decision.Reason);
    }

    /// <summary>
    /// The record the scenario would have written. Stage 2 reads these lines out
    /// of a real session, so their shape is part of the interface.
    /// </summary>
    [Fact]
    public void The_record_carries_the_counter_and_the_window_and_the_handover()
    {
        var lines = new List<string>();
        var recorder = new DebugRecorder(line =>
        {
            lines.Add(line);
            return true;
        });

        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, ListedPrice, NoChanceFloor, BethWithACliff());

        CounterofferDecision send = CounterofferGate.Send(
            new ContractClaimRegistry().Claim(BethsContract),
            found.Planned, found.Chance, Offered, NinetyPerCent);

        recorder.Record(DebugRow.Negotiation(
            send.Outcome.ToString(), acted: true, send.Reason, "Beth Penn", BethsContract,
            "ogkush", found.Planned.Quantity, found.Planned.TotalPrice, found.Chance, Offered));

        recorder.Record(DebugRow.Scheduling(
            ScheduleOutcome.Schedule.ToString(), acted: true,
            "Afternoon is the next window you allow", "Beth Penn", BethsContract,
            DealWindowName.Of(DealWindow.Afternoon)));

        recorder.Record(DebugRow.Handover(
            HandoverAction.HandOver.ToString(), acted: true,
            "Beth Penn is at the deal location, inside the window, and the game calls the handover valid",
            "Beth Penn", BethsContract, "ogkush",
            QualityTier.Standard, packages: 4, units: Order, contractPayment: Counter));

        Assert.Equal(3, lines.Count);

        Assert.Contains("\"decision\":\"negotiation\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"price\":760", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"offered_payment\":640", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"quantity\":8", lines[0], StringComparison.Ordinal);

        Assert.Contains("\"decision\":\"scheduling\"", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"window\":\"Afternoon\"", lines[1], StringComparison.Ordinal);

        Assert.Contains("\"decision\":\"handover\"", lines[2], StringComparison.Ordinal);
        Assert.Contains("\"units\":8", lines[2], StringComparison.Ordinal);
        Assert.Contains("\"contract_payment\":760", lines[2], StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Divergence.</b> "What I must be able to see" names three readings on
    /// the app, and the app draws none of them. <c>AutomationForm</c> says so
    /// itself — <i>"Nothing on this page is a reading"</i> — and the HANDOVER
    /// block's per-contract rows were deleted with their reasons:
    /// <i>"Ich werde doch nie da reingucken."</i>
    /// </summary>
    /// <remarks>
    /// Asserted as absence so that it goes red the day somebody builds it, which
    /// is the day the scenario has to be read again rather than a day this test
    /// should quietly keep passing.
    /// </remarks>
    [Fact]
    public void Nothing_on_the_app_says_running_or_how_far_away_I_am_or_carrying_zero_of_eight()
    {
        TheScenarioFile.Says(Scenario, "On the Automation tab, the Negotiating block says `running`.");
        TheScenarioFile.Says(Scenario, "Before I get there, Beth's row says how far away I am.");
        TheScenarioFile.Says(Scenario, "her row says `carrying 0 of 8`");

        AutomationForm page = AFullyConfiguredPage.Of(
            new AdvisorSettings { AutoCounterOffer = true, AutoHandover = true },
            AnEveningOfPlay.Allowing(DealWindow.Morning, DealWindow.Afternoon));

        // There is no Negotiating block; the block is PRICE NEGOTIATION, and no
        // row of it is a status.
        Assert.DoesNotContain(page.Blocks, block => block.Title == "Negotiating");

        foreach (AutomationBlock block in page.Blocks)
        {
            Assert.DoesNotContain(block.Rows, row =>
                row.Text.Contains("running", StringComparison.OrdinalIgnoreCase)
                || row.Value.Contains("running", StringComparison.OrdinalIgnoreCase)
                || row.Text.Contains("carrying", StringComparison.OrdinalIgnoreCase)
                || row.Text.Contains("Beth", StringComparison.OrdinalIgnoreCase));
        }
    }
}
