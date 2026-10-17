using static Dealcraft.Core.Tests.Floors;
using System;
using System.Collections.Generic;
using Xunit;

namespace Dealcraft.Core.Tests.Scenarios;

/// <summary>
/// Scenario 3: the same customer, with the floor raised to $80, and the one
/// sentence that is the whole point of it.
/// </summary>
[Drives(3, "The same thing, when my floor makes it impossible")]
public class ScenarioThreeTests
{
    private const int Scenario = 3;

    /// <summary>"as scenario 2, but OG Kush is listed at **$80**".</summary>
    private const float ListedPrice = 80f;

    private const int Order = 3;

    private const float Offered = 120f;

    private const float NinetyPerCent = 0.9f;

    private static readonly string ChloesContract =
        ContractKey.ForOffer("chloe_bowers", elapsedDays: 12, timeOfDay: 1130);

    /// <summary>The same Chloe as scenario 2: $55 a unit at ninety percent.</summary>
    private static Func<int, float, float> Chloe() =>
        AnEveningOfPlay.ACustomerWhoFallsAway(
            certainUpTo: Offered, anchorTotal: 165f, anchorChance: NinetyPerCent, zeroAt: 175f);

    /// <summary>
    /// Steps 2 and 3: no price at or above $80 a unit is one she would take, so
    /// nothing is sent — countering would spend the $120 already on the table.
    /// </summary>
    [Fact]
    public void Nothing_is_sent_because_countering_would_cost_the_hundred_and_twenty()
    {
        TheScenarioFile.Says(Scenario, "There is no price at or above $80 a unit that Chloe is 90% likely to accept.");
        TheScenarioFile.Says(Scenario, "**Dealcraft sends nothing.** It does not counter at a price she will refuse,\n   because refusing costs me the $120 that was already on the table.");

        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, ListedPrice, NoChanceFloor, Chloe());

        Assert.False(found.Planned.HasOffer);

        CounterofferDecision send = CounterofferGate.Send(
            new ContractClaimRegistry().Claim(ChloesContract),
            found.Planned, found.Chance, Offered, NinetyPerCent);

        Assert.Equal(CounterofferOutcome.Skip, send.Outcome);
    }

    /// <summary>
    /// The same sentence, the other way it bites. "Refusing costs me the $120
    /// that was already on the table" is a statement about expected value, and
    /// the gate is built out of exactly that quantity: a counter can clear the
    /// floor, clear the chance the player set, be a bigger number than the
    /// standing offer, and still be worth less than it — because sending it
    /// clears the responses that would have accepted the standing offer.
    /// </summary>
    /// <remarks>
    /// Driven with the price listed low enough that the floor does not decide
    /// it, so that the comparison the sentence is about is the one that fires.
    /// The rule it exercises is the money fix in
    /// <see cref="CounterofferGate.Send"/>: at the shipped defaults the rule it
    /// replaced lost money.
    /// </remarks>
    [Fact]
    public void A_bigger_counter_worth_less_than_the_offer_is_not_sent_either()
    {
        TheScenarioFile.Says(Scenario, "because refusing costs me the $120 that was already on the table.");

        // Chloe will pay a little more than she offered — but not likely enough
        // for the bet to beat certain money. She is not certain at her own offer
        // either, which is how the two figures come apart: the game generated
        // the offer, it did not accept it.
        Func<int, float, float> barelyMore = AnEveningOfPlay.ACustomerWhoFallsAway(
            certainUpTo: 100f, anchorTotal: 130f, anchorChance: NinetyPerCent, zeroAt: 140f);

        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, floorPerUnit: 40f, NoChanceFloor, barelyMore);

        Assert.True(found.Planned.HasOffer);
        Assert.True(
            found.Chance >= NinetyPerCent,
            "the floor is not what refuses this one; the expected value is");

        // A bigger number than the standing offer, and worth less than it.
        Assert.True(found.Planned.TotalPrice > Offered);
        Assert.True(found.Chance * found.Planned.TotalPrice <= Offered);

        CounterofferDecision send = CounterofferGate.Send(
            new ContractClaimRegistry().Claim(ChloesContract),
            found.Planned, found.Chance, Offered, NinetyPerCent);

        Assert.Equal(CounterofferOutcome.Skip, send.Outcome);
        Assert.Contains("already on the table", send.Reason, StringComparison.Ordinal);
        Assert.Contains("sending it takes that offer away", send.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Step 4: the sentence. "That one line is the whole point. I can see why
    /// nothing is happening without opening a log file, and I can fix it by
    /// lowering the price or the confidence."
    /// </summary>
    /// <remarks>
    /// The sentence is real and it does more than the scenario asks: it names the
    /// price the curve did clear and the one move that would take it, so the fix
    /// the scenario describes is spelled out rather than left to be worked out.
    /// Where it is <em>said</em> is the divergence, and it is below.
    /// </remarks>
    [Fact]
    public void The_reason_begins_with_the_words_the_scenario_quotes()
    {
        TheScenarioFile.Says(Scenario, "`nothing clears your floor`");

        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, ListedPrice, NoChanceFloor, Chloe());

        Assert.StartsWith("nothing clears your floor", found.Planned.Reason, StringComparison.Ordinal);
        Assert.Contains($"of {ListedPrice:0.##} per unit", found.Planned.Reason, StringComparison.Ordinal);

        // And it says what would take the deal, which is the fix the scenario
        // says the player should be able to make.
        Assert.Contains("so lower the listed price to", found.Planned.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// And it survives into the record, which is where stage 2 looks for it.
    /// </summary>
    [Fact]
    public void The_record_carries_that_reason_with_the_chance_that_argued_it()
    {
        var lines = new List<string>();
        var recorder = new DebugRecorder(line =>
        {
            lines.Add(line);
            return true;
        });

        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, ListedPrice, NoChanceFloor, Chloe());

        CounterofferDecision send = CounterofferGate.Send(
            new ContractClaimRegistry().Claim(ChloesContract),
            found.Planned, found.Chance, Offered, NinetyPerCent);

        // The loop records a search that planned nothing with the chance it
        // found and a null where there was no price to send.
        Assert.True(recorder.Record(DebugRow.Negotiation(
            send.Outcome.ToString(), acted: false, send.Reason, "Chloe Bowers", ChloesContract,
            "ogkush", quantity: null, price: null, chance: found.Chance, offeredPayment: Offered)));

        string row = Assert.Single(lines);

        Assert.Contains("\"acted\":false", row, StringComparison.Ordinal);
        Assert.Contains("\"reason\":\"nothing clears your floor", row, StringComparison.Ordinal);
        Assert.Contains("\"quantity\":null", row, StringComparison.Ordinal);
        Assert.Contains("\"price\":null", row, StringComparison.Ordinal);
        Assert.Contains("\"offered_payment\":120", row, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Divergence.</b> The scenario puts that sentence on the Negotiating
    /// block, so that it can be read "without opening a log file". No block on
    /// the Automation tab carries a reading: <see cref="AutomationForm"/> says
    /// <i>"Nothing on this page is a reading … they go to the debug file and
    /// nowhere else"</i>, and there is no block called Negotiating either.
    /// </summary>
    [Fact]
    public void No_block_on_the_page_carries_that_sentence()
    {
        TheScenarioFile.Says(Scenario, "The Negotiating block says `nothing clears your floor`.");
        TheScenarioFile.Says(Scenario, "I can see why nothing is happening without\nopening a log file");

        AutomationForm page = AFullyConfiguredPage.Of(
            new AdvisorSettings { AutoCounterOffer = true },
            AnEveningOfPlay.Allowing(DealWindow.Afternoon));

        Assert.DoesNotContain(page.Blocks, block => block.Title == "Negotiating");
        Assert.Contains(page.Blocks, block => block.Title == "PRICE NEGOTIATION");

        foreach (AutomationBlock block in page.Blocks)
        {
            Assert.DoesNotContain(block.Rows, row =>
                row.Text.Contains("clears your floor", StringComparison.OrdinalIgnoreCase)
                || row.Value.Contains("clears your floor", StringComparison.OrdinalIgnoreCase)
                || row.Hint.Contains("clears your floor", StringComparison.OrdinalIgnoreCase));
        }
    }
}
