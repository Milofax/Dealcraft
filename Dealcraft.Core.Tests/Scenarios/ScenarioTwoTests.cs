using System;
using System.Collections.Generic;
using Xunit;

namespace Dealcraft.Core.Tests.Scenarios;

/// <summary>
/// Scenario 2, driven: the floor holds, and the jar goes as a jar.
/// </summary>
/// <remarks>
/// <para>
/// <b>This scenario has two premises the rebuild took away.</b> Its settings
/// name <c>use whole packages, wasting up to 4 units</c>, which is not a setting
/// and has no equivalent; and its story joins the negotiation to the pocket,
/// which the mod does not — <see cref="CounterofferPlan.Search"/> prices the
/// quantity on the table and never raises it, and the packaging is read hours
/// later by a different pass that never sees the conversation.
/// </para>
/// <para>
/// What the scenario is <em>about</em> survives both: the floor holds at
/// <c>$55</c> a unit, and a jar that cannot be split goes out whole. Both are
/// driven below. The quantity and the waste limit are asserted as divergences.
/// </para>
/// </remarks>
[Drives(2, "A whole jar out of the door, with my price floor holding")]
public class ScenarioTwoTests
{
    private const int Scenario = 2;

    private const float ListedPrice = 52f;

    /// <summary>Chloe wants three, for $120 — forty dollars a unit.</summary>
    private const int Order = 3;

    private const float Offered = 120f;

    /// <summary>The per-unit price the scenario wants out of the negotiation.</summary>
    private const float FiftyFive = 55f;

    private const float NinetyPerCent = 0.9f;

    private static readonly string ChloesContract =
        ContractKey.ForOffer("chloe_bowers", elapsedDays: 12, timeOfDay: 1130);

    /// <summary>
    /// Chloe: certain at what she offered, and $55 a unit is the most she takes
    /// at ninety percent.
    /// </summary>
    private static Func<int, float, float> Chloe() =>
        AnEveningOfPlay.ACustomerWhoFallsAway(
            certainUpTo: Offered,
            anchorTotal: FiftyFive * Order,
            anchorChance: NinetyPerCent,
            zeroAt: 175f);

    /// <summary>A jar of five, and nothing smaller.</summary>
    private static IReadOnlyList<CarriedLot> AJarOfFive() =>
        new[] { new CarriedLot(QualityTier.Standard, unitsPerPackage: 5, packages: 1) };

    /// <summary>Step 1 and 2: forty dollars a unit is under the floor, so it is not answered.</summary>
    [Fact]
    public void Forty_dollars_a_unit_is_below_the_floor_and_is_not_answered_at()
    {
        TheScenarioFile.Says(Scenario, "she wants **3 OG Kush for $120**. That is $40 each —\n   below my floor.");
        TheScenarioFile.Says(Scenario, "Dealcraft will not answer at $40 a unit.");

        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, ListedPrice, Chloe());

        Assert.True(found.Planned.HasOffer);
        Assert.True(
            found.Planned.PricePerUnit >= ListedPrice,
            $"the counter is {found.Planned.PricePerUnit} a unit, under the listed {ListedPrice}");
    }

    /// <summary>
    /// Step 3, in the half the mod still does: it counters at $55 a unit, which
    /// clears the floor and which Chloe takes at ninety percent, once.
    /// </summary>
    [Fact]
    public void It_counters_at_fifty_five_a_unit_and_sends_it_once()
    {
        TheScenarioFile.Says(Scenario, "($55 each), which clears my floor and which\n   Chloe takes with a 90% chance. It sends that once.");

        var claims = new ContractClaimRegistry();

        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, ListedPrice, Chloe());

        Assert.Equal(FiftyFive, found.Planned.PricePerUnit, 2);
        Assert.Equal(NinetyPerCent, found.Chance, 3);

        CounterofferDecision send = CounterofferGate.Send(
            claims.Claim(ChloesContract), found.Planned, found.Chance, Offered, NinetyPerCent);

        Assert.Equal(CounterofferOutcome.Send, send.Outcome);

        CounterofferDecision again = CounterofferGate.Send(
            claims.Claim(ChloesContract), found.Planned, found.Chance, Offered, NinetyPerCent);

        Assert.Equal(CounterofferOutcome.Skip, again.Outcome);
    }

    /// <summary>
    /// <b>Divergence.</b> The scenario counters for <c>5 at $275</c>. The mod
    /// prices the quantity on the table and cannot raise it:
    /// <see cref="CounterofferPlan.Search"/> returns the single quantity the
    /// customer asked for, and its own remark says the switch that searched
    /// every quantity and the target quantity that capped it are both deleted —
    /// <i>"a second goal the player could not see was the only thing they
    /// served."</i>
    /// </summary>
    [Fact]
    public void The_quantity_is_never_raised_from_three_to_five()
    {
        TheScenarioFile.Says(Scenario, "It negotiates for **5 at $275**");

        QuantityRange searched = CounterofferPlan.Search(
            AnEveningOfPlay.TheCounterofferScreen, quantityOnScreen: Order);

        Assert.True(searched.IsSingle);
        Assert.Equal(Order, searched.Lowest);
        Assert.Equal(Order, searched.Highest);

        Negotiation found = NegotiationSearch.For(
            AnEveningOfPlay.TheCounterofferScreen, Order, ListedPrice, Chloe());

        Assert.Equal(Order, found.Planned.Quantity);
        Assert.NotEqual(5, found.Planned.Quantity);
    }

    /// <summary>
    /// Step 4: the jar goes as a jar. Three units cannot leave a pocket holding
    /// nothing smaller than five, and that is not a refusal — packages cannot be
    /// split, and losing the deal to save two units is much the larger loss.
    /// </summary>
    [Fact]
    public void The_jar_goes_as_a_jar()
    {
        TheScenarioFile.Says(Scenario, "I am carrying a **jar of five**,\n   nothing smaller, so three units cannot leave my pockets anyway.");
        TheScenarioFile.Says(Scenario, "The jar goes as a jar");

        GradeFill fill = PackagePlan.Choose(
            AJarOfFive(), requestedQuality: QualityTier.Standard, order: Order, GradeReach.Exactly);

        Assert.True(fill.Covers);
        Assert.Equal(5, fill.Units);
        Assert.Equal(1, fill.Packages);
    }

    /// <summary>
    /// <b>Divergence, and it is the opposite of what the scenario says.</b> "If
    /// the jar were a brick … Dealcraft does not round up to it." It does.
    /// <see cref="PackagePlan"/> states the rule in as many words —
    /// <i>"Overshoot is never a reason to refuse"</i> — because losing the deal
    /// to save units is the larger loss and the player is standing there.
    /// There is no waste limit for twenty units to be past, which is the
    /// scenario's other missing premise.
    /// </summary>
    [Fact]
    public void A_brick_of_twenty_does_go_out_against_an_order_of_three()
    {
        TheScenarioFile.Says(Scenario, "far past my limit of\nfour. Dealcraft does not round up to it.");

        var aBrick = new[] { new CarriedLot(QualityTier.Standard, unitsPerPackage: 20, packages: 1) };

        GradeFill fill = PackagePlan.Choose(aBrick, QualityTier.Standard, Order, GradeReach.Exactly);

        Assert.True(fill.Covers);
        Assert.Equal(20, fill.Units);
        Assert.Equal(PackageFillOutcome.Covered, fill.Fill.Outcome);
    }

    /// <summary>
    /// <b>Divergence.</b> <c>wasting up to N units</c> is named in this
    /// scenario's settings and is not a setting. It is on no page, and it is not
    /// among the settings <see cref="AutomationForm.FileOnly"/> names as living
    /// in the file alone, so it exists nowhere.
    /// </summary>
    [Fact]
    public void There_is_no_setting_for_wasting_up_to_so_many_units()
    {
        TheScenarioFile.Says(Scenario, "Handing over: on — *use whole packages, wasting up to 4 units*");

        AutomationForm page = AFullyConfiguredPage.Of(
            new AdvisorSettings { AutoCounterOffer = true, AutoHandover = true },
            AnEveningOfPlay.Allowing(DealWindow.Afternoon));

        Assert.DoesNotContain(
            AutomationForm.FileOnly,
            key => key.Contains("wasting", StringComparison.OrdinalIgnoreCase)
                || key.Contains("waste", StringComparison.OrdinalIgnoreCase));

        foreach (AutomationBlock block in page.Blocks)
        {
            Assert.DoesNotContain(block.Rows, row =>
                row.Text.Contains("wasting", StringComparison.OrdinalIgnoreCase)
                || row.Hint.Contains("wasting", StringComparison.OrdinalIgnoreCase));
        }

        // What the handover block does ask is about the grade, not about waste,
        // and its footnote says the packaging may overshoot whichever way it is
        // answered.
        AutomationBlock handover = Assert.Single(page.Blocks, block => block.Title == "HANDOVER");
        Assert.Contains(handover.Rows, row => row.Text == "Exactly the grade that was ordered");
        Assert.Contains(handover.Rows, row =>
            row.Text == "* package sizes may still overshoot — packages cannot be split");
    }
}
