using System;
using System.Collections.Generic;
using Xunit;

namespace Dealcraft.Core.Tests.Scenarios;

/// <summary>
/// Scenario 4: what leaves the pockets when only the bigger packaging is in
/// them.
/// </summary>
/// <remarks>
/// <b>The scenario's three cases are two.</b> Its A and B differ only by
/// <c>wasting up to N units</c>, which is not a setting, and the answer the mod
/// gives is A's under every setting it does have — because the packing already
/// minimises the giveaway and never needed a number to be told to.
/// </remarks>
[Drives(4, "Handing over when I only have the bigger packaging")]
public class ScenarioFourTests
{
    private const int Scenario = 4;

    /// <summary>"Chloe's contract asks for **4**."</summary>
    private const int Order = 4;

    /// <summary>Four baggies and one jar of five.</summary>
    private static IReadOnlyList<CarriedLot> FourBaggiesAndAJar() => new[]
    {
        new CarriedLot(QualityTier.Standard, unitsPerPackage: 1, packages: 4),
        new CarriedLot(QualityTier.Standard, unitsPerPackage: 5, packages: 1),
    };

    private static IReadOnlyList<CarriedLot> OnlyTheJar() =>
        new[] { new CarriedLot(QualityTier.Standard, unitsPerPackage: 5, packages: 1) };

    /// <summary>
    /// Case A: exactly four leave the pockets, as the four baggies.
    /// </summary>
    [Fact]
    public void The_four_baggies_go_and_exactly_four_leave_my_pockets()
    {
        TheScenarioFile.Says(Scenario, "Dealcraft hands over the **four baggies**. Exactly four leave my pockets.");

        GradeFill fill = PackagePlan.Choose(
            FourBaggiesAndAJar(), QualityTier.Standard, Order, GradeReach.Exactly);

        Assert.True(fill.Covers);
        Assert.Equal(Order, fill.Units);

        // Four baggies out of the first lot, and the jar untouched.
        Assert.Equal(4, fill.Fill.From(0));
        Assert.Equal(0, fill.Fill.From(1));
    }

    /// <summary>
    /// <b>Divergence.</b> Case B says the same pockets and the same contract
    /// give the jar of five once four units of waste are allowed. They do not,
    /// under either answer the mod has: the packing ranks by fewest total units
    /// first, and four is fewer than five whatever is set.
    /// </summary>
    /// <remarks>
    /// Both of the mod's settings are driven, so this is not an argument about
    /// which one <c>wasting up to 4 units</c> would have mapped to. Neither
    /// gives the jar.
    /// </remarks>
    [Fact]
    public void Case_B_gives_the_four_baggies_too_under_every_setting_the_mod_has()
    {
        TheScenarioFile.Says(Scenario, "Dealcraft hands over the **jar of five**. Five\nleave my pockets");

        foreach (GradeReach reach in new[] { GradeReach.Exactly, GradeReach.MayUseAHigherGrade })
        {
            GradeFill fill = PackagePlan.Choose(
                FourBaggiesAndAJar(), QualityTier.Standard, Order, reach);

            Assert.True(fill.Covers);
            Assert.Equal(Order, fill.Units);
            Assert.NotEqual(5, fill.Units);
        }
    }

    /// <summary>
    /// Case C: only the jar, and packaging cannot be split, so the jar goes
    /// either way — five units for a contract of four.
    /// </summary>
    [Fact]
    public void With_only_the_jar_it_goes_either_way()
    {
        TheScenarioFile.Says(Scenario, "Packaging cannot be split, so the jar goes\neither way.");

        foreach (GradeReach reach in new[] { GradeReach.Exactly, GradeReach.MayUseAHigherGrade })
        {
            GradeFill fill = PackagePlan.Choose(OnlyTheJar(), QualityTier.Standard, Order, reach);

            Assert.True(fill.Covers);
            Assert.Equal(5, fill.Units);
            Assert.Equal(1, fill.Packages);
        }
    }

    /// <summary>
    /// <b>Divergence.</b> The scenario wants a contract row reading
    /// <c>5 units for 4</c>. That row was deleted with the rest of the HANDOVER
    /// block's per-contract list — the game already lists contracts down the
    /// left of the screen, and <i>"komplett raus ohne Ersatz"</i>. What the mod
    /// says instead is a log line, in
    /// <c>AutomaticHandover</c>: <c>5 units for a contract of 4: nothing smaller
    /// in your bag.</c>
    /// </summary>
    [Fact]
    public void There_is_no_row_saying_five_units_for_four()
    {
        TheScenarioFile.Says(Scenario, "the row says\n`5 units for 4`");

        AutomationForm page = AFullyConfiguredPage.Of(
            new AdvisorSettings { AutoHandover = true },
            AnEveningOfPlay.Allowing(DealWindow.Afternoon));

        AutomationBlock handover = Assert.Single(page.Blocks, block => block.Title == "HANDOVER");

        // Two answers and a footnote. No contract is named, because no contract
        // is listed here any more.
        Assert.DoesNotContain(handover.Rows, row =>
            row.Text.Contains("units for", StringComparison.OrdinalIgnoreCase)
            || row.Value.Contains("units for", StringComparison.OrdinalIgnoreCase));

        // What it says instead. Read as text because the line is written on the
        // far side of the seam, which is what AdapterSource exists for.
        Assert.Contains(
            "units for a contract of ",
            AdapterSource.Read("AutomaticHandover.cs"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// And the record carries what the packaging forced, which is where a
    /// session's own account of it lives.
    /// </summary>
    [Fact]
    public void The_record_says_five_units_went_for_a_contract_of_four()
    {
        var lines = new List<string>();
        var recorder = new DebugRecorder(line =>
        {
            lines.Add(line);
            return true;
        });

        GradeFill fill = PackagePlan.Choose(OnlyTheJar(), QualityTier.Standard, Order, GradeReach.Exactly);

        recorder.Record(DebugRow.Handover(
            HandoverAction.HandOver.ToString(), acted: true,
            "Chloe Bowers is at the deal location, inside the window, and the game calls the handover valid",
            "Chloe Bowers", ContractKey.ForOffer("chloe_bowers", 12, 1130), "ogkush",
            grade: QualityTier.Standard,
            packages: fill.Packages,
            units: fill.Units,
            contractPayment: 220f));

        string row = Assert.Single(lines);

        Assert.Contains("\"units\":5", row, StringComparison.Ordinal);
        Assert.Contains("\"packages\":1", row, StringComparison.Ordinal);
        Assert.Contains("\"grade_name\":\"Standard\"", row, StringComparison.Ordinal);
    }
}
