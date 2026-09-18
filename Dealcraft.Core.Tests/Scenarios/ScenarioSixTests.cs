using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Dealcraft.Core.Tests.Scenarios;

/// <summary>
/// Scenario 6: the price that costs customers without ever producing a refusal.
/// </summary>
/// <remarks>
/// <para>
/// <b>The arithmetic is all here; the row is nowhere.</b> The Products tab was
/// deleted last night, and <c>ListedPricePass</c> states the cost of that in as
/// many words: <c>LIST IT AT</c> was the only place a listed price that silently
/// costs customers was visible, and with the tab gone the price is visible
/// nowhere in the game.
/// </para>
/// <para>
/// So every one of the row's four figures is driven out of the shipped code
/// below, and the row itself is asserted absent. That is the shape of this
/// scenario now: the mod still knows the thing; nothing shows it.
/// </para>
/// </remarks>
[Drives(6, "A price that has quietly cost me customers")]
public class ScenarioSixTests
{
    private const int Scenario = 6;

    private const float Listed = 52f;

    private const float Suggested = 48f;

    private const float Clears = 41f;

    /// <summary>
    /// Fourteen customers who could order OG Kush, each with the ceiling the
    /// curve found for them and the size the game says they would order.
    /// </summary>
    /// <remarks>
    /// The ceilings are the scenario's own data rather than a guess: three of
    /// them sit under $52, all fourteen sit at or above $41, and $48 is the
    /// price that takes most across the fourteen. Those three facts are the
    /// scenario's four figures, and the roster is the smallest thing that holds
    /// all of them at once.
    /// </remarks>
    private static IReadOnlyList<ProductCandidate> Fourteen()
    {
        float[] ceilings = { 41, 44, 48, 52, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100 };

        return ceilings
            .Select((ceiling, index) => new ProductCandidate(
                customerName: $"customer {index + 1}",
                orderQuantity: 10,
                highestAcceptableTotal: ceiling * 10f,
                appeal: 0f))
            .ToArray();
    }

    /// <summary>
    /// Step 3: three of the fourteen cannot afford a unit at $52, so they never
    /// text at all.
    /// </summary>
    [Fact]
    public void Three_of_fourteen_are_priced_out_at_fifty_two()
    {
        TheScenarioFile.Says(Scenario, "Three of my fourteen customers cannot afford a single unit at $52 per\n   order");
        TheScenarioFile.Says(Scenario, "priced out 3 of 14");

        IReadOnlyList<ProductCandidate> roster = Fourteen();

        ListedPricePoint atFiftyTwo = PricingRecommendation.At(Listed, roster);

        Assert.Equal(14, roster.Count);
        Assert.Equal(11, atFiftyTwo.CustomersReached);
        Assert.Equal(3, roster.Count - atFiftyTwo.CustomersReached);
    }

    /// <summary>
    /// Step 4: drop the price to $41 and nobody is priced out.
    /// </summary>
    [Fact]
    public void At_forty_one_nobody_is_priced_out()
    {
        TheScenarioFile.Says(Scenario, "I drop the price to $41 and the row reads `priced out 0 of 14`.");

        IReadOnlyList<ProductCandidate> roster = Fourteen();

        ListedPricePoint atFortyOne = PricingRecommendation.At(Clears, roster);

        Assert.Equal(roster.Count, atFortyOne.CustomersReached);
        Assert.Equal(0, roster.Count - atFortyOne.CustomersReached);
    }

    /// <summary>
    /// The row's other two figures: what the mod suggests, and the price that
    /// clears everybody.
    /// </summary>
    [Fact]
    public void The_suggested_price_is_forty_eight_and_the_price_that_clears_is_forty_one()
    {
        TheScenarioFile.Says(Scenario, "`listed $52 · suggested $48 · clears $41 · priced out 3 of 14`");

        PricingRecommendation best = PricingRecommendation.Best(Fourteen());

        Assert.True(best.Available);
        Assert.Equal(Suggested, best.PricePerUnit);

        // "clears": the cheapest rung of the ladder, which is the lowest ceiling
        // anybody has and therefore the price nobody is priced out of.
        ListedPricePoint cheapest = best.Points[0];
        Assert.Equal(Clears, cheapest.PricePerUnit);
        Assert.Equal(14, cheapest.CustomersReached);

        // And the suggestion is a suggestion because it takes most, not because
        // it reaches most: it reaches twelve and $41 reaches fourteen.
        Assert.Equal(12, best.CustomersReached);
        Assert.True(best.WeeklyTakings > cheapest.WeeklyTakings);
    }

    /// <summary>
    /// And the decision the mod would take off those figures, with the sentence
    /// it argues it in.
    /// </summary>
    [Fact]
    public void Turned_on_it_writes_forty_eight_and_says_what_that_costs_and_reaches()
    {
        PricingRecommendation best = PricingRecommendation.Best(Fourteen());

        ListedPriceDecision maintained = ListedPriceDecision.For(
            maintaining: true, "OG Kush", Listed, best);

        Assert.Equal(ListedPriceOutcome.Write, maintained.Outcome);
        Assert.Equal(Suggested, maintained.Price);
        Assert.Equal("OG Kush: $52 becomes $48, which 12 of your customers still clear ($5760 a week)", maintained.Reason);

        // Left alone, it says whose prices they are.
        ListedPriceDecision manual = ListedPriceDecision.For(
            maintaining: false, "OG Kush", Listed, best);

        Assert.Equal(ListedPriceOutcome.LeaveAlone, manual.Outcome);
        Assert.Equal("you set your own prices", manual.Reason);
    }

    /// <summary>
    /// <b>Divergence.</b> There is no Products tab and no row. The four figures
    /// above reach a person only through the decision record, which is a file.
    /// </summary>
    [Fact]
    public void Nothing_in_the_game_draws_that_row()
    {
        TheScenarioFile.Says(Scenario, "On the Products tab its row reads:");

        AutomationForm page = AFullyConfiguredPage.Of(
            new AdvisorSettings(), AnEveningOfPlay.Allowing(DealWindow.Afternoon),
            maintainingPrices: true);

        Assert.DoesNotContain(page.Blocks, block => block.Title == "Products");

        foreach (AutomationBlock block in page.Blocks)
        {
            Assert.DoesNotContain(block.Rows, row =>
                row.Text.Contains("priced out", StringComparison.OrdinalIgnoreCase)
                || row.Value.Contains("priced out", StringComparison.OrdinalIgnoreCase)
                || row.Text.Contains("LIST IT AT", StringComparison.Ordinal)
                || row.Value.Contains("suggested", StringComparison.OrdinalIgnoreCase));
        }

        // The tab is gone from the adapter too, which is what makes the absence
        // above a deletion rather than a page that happens not to be built.
        Assert.DoesNotContain("LIST IT AT\"", AdapterSource.ReadAll(), StringComparison.Ordinal);
    }

    /// <summary>
    /// What is left instead: a record row carrying both prices and the sentence
    /// that argued the move. Stage 2 reads these.
    /// </summary>
    [Fact]
    public void The_record_carries_both_prices_and_the_reason()
    {
        var lines = new List<string>();
        var recorder = new DebugRecorder(line =>
        {
            lines.Add(line);
            return true;
        });

        ListedPriceDecision decision = ListedPriceDecision.For(
            maintaining: true, "OG Kush", Listed, PricingRecommendation.Best(Fourteen()));

        recorder.Record(DebugRow.ListedPrice(
            decision.Outcome.ToString(), acted: true, decision.Reason,
            "ogkush", oldPrice: Listed, newPrice: decision.Price));

        string row = Assert.Single(lines);

        Assert.Contains("\"decision\":\"listed_price\"", row, StringComparison.Ordinal);
        Assert.Contains("\"old_price\":52", row, StringComparison.Ordinal);
        Assert.Contains("\"new_price\":48", row, StringComparison.Ordinal);
        Assert.Contains("12 of your customers still clear", row, StringComparison.Ordinal);
    }
}
