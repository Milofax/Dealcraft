using System.Text.RegularExpressions;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// <c>Exactly the grade that was ordered</c>: the one behaviour <c>app.md</c>
/// adds rather than deletes, and the default.
/// </summary>
/// <remarks>
/// <para>
/// The packing search used to start at the contract's grade and walk upward,
/// taking the lowest grade that covered the order. So a jar of Heavenly went out
/// on a contract that asked for Poor whenever that was the only thing that
/// covered it — the owner's best product spent at a Poor price, and nobody chose
/// it. Story 23: <em>"I want exactly what was ordered to be the default, so that
/// my best product is never spent on a contract that asked for less."</em>
/// </para>
/// <para>
/// It is about the grade and nothing else. Packages cannot be split, so a
/// delivery may still overshoot in units under either answer, and the cases below
/// say so where they can fail.
/// </para>
/// </remarks>
public class ExactlyTheGradeOrderedTests
{
    private const int Baggie = 1;
    private const int Jar = 5;
    private const int Brick = 20;

    /// <summary>
    /// The case the setting exists for, both ways round: a jar of Heavenly in the
    /// bag against a contract asking for Poor. Nothing in exact mode; the jar
    /// when the player allows a higher grade.
    /// </summary>
    [Fact]
    public void A_jar_of_heavenly_does_not_go_out_on_a_contract_that_asked_for_poor()
    {
        var bag = new[] { new CarriedLot(QualityTier.Heavenly, Jar, 1) };

        GradePlan exactly = Plan(QualityTier.Poor, 5, GradeReach.Exactly, out PackageFill nothing, bag);
        GradePlan roundedUp = Plan(
            QualityTier.Poor, 5, GradeReach.MayUseAHigherGrade, out PackageFill jar, bag);

        Assert.False(exactly.CanDeliver);
        Assert.Equal(0, nothing.Units);

        Assert.True(roundedUp.CanDeliver);
        Assert.Equal(QualityTier.Heavenly, roundedUp.ChosenQuality);
        Assert.Equal(5, jar.Units);
        Assert.Equal(1, jar.Packages);
    }

    /// <summary>
    /// And the silence says why it was silent. The player cannot see from inside
    /// the game that a better grade was in the bag and was left there, so the
    /// reason names the grade and the setting rather than describing a shortage
    /// that is not there.
    /// </summary>
    [Fact]
    public void The_refusal_names_the_grade_it_held_back_and_the_setting_that_held_it()
    {
        GradePlan plan = Plan(
            QualityTier.Poor,
            5,
            GradeReach.Exactly,
            out PackageFill _,
            new CarriedLot(QualityTier.Heavenly, Jar, 1));

        Assert.False(plan.CanDeliver);
        Assert.Contains("Heavenly has them", plan.Reason);
        Assert.Contains("exactly the grade that was ordered", plan.Reason);
        Assert.DoesNotContain("no single grade covers", plan.Reason);
    }

    /// <summary>
    /// That reason is what reaches the debug file: the handover pass copies the
    /// plan's own sentence into the record rather than re-wording it, and the
    /// record is the only account there is of a handover that did not happen.
    /// </summary>
    /// <remarks>
    /// Read out of the adapter's source, because the pass calls into the running
    /// game and no test here may follow it there. See <see cref="AdapterSource"/>.
    /// </remarks>
    [Fact]
    public void The_handover_pass_records_the_plans_own_reason()
    {
        string pass = AdapterSource.Read("AutomaticHandover.cs");

        Assert.Matches(
            new Regex(@"Record\(\s*HandoverAction\.Refuse,\s*name,\s*key,\s*contract,\s*delivery\.Why\)"),
            pass);

        // And the answers it plans against are the player's, read afresh with
        // the rest of the settings on every scan: which grade may go out, and
        // where the handover may happen.
        Assert.Matches(new Regex(@"Attempt\([^)]*settings\.Reach, settings\.Place\)"), pass);
    }

    /// <summary>
    /// Exact mode is not a refusal to deliver: where the contract's own grade is
    /// in the bag it goes out exactly as it always did, and the better grade
    /// beside it is left alone under both answers.
    /// </summary>
    [Theory]
    [InlineData(GradeReach.Exactly)]
    [InlineData(GradeReach.MayUseAHigherGrade)]
    public void The_grade_that_was_ordered_is_delivered_under_either_answer(GradeReach reach)
    {
        GradePlan plan = Plan(
            QualityTier.Standard,
            10,
            reach,
            out PackageFill fill,
            new CarriedLot(QualityTier.Standard, Jar, 2),
            new CarriedLot(QualityTier.Heavenly, Brick, 1));

        Assert.True(plan.CanDeliver);
        Assert.Equal(QualityTier.Standard, plan.ChosenQuality);
        Assert.Equal(10, fill.Units);
        Assert.Equal(2, fill.Packages);
    }

    /// <summary>
    /// Neither answer changes what happens to package size. An order of six
    /// against two jars of five can only be met with ten, and refusing that to
    /// save four units would lose the deal — <c>PackagePlan</c> argues it out in
    /// as many words. Exactness is about the grade alone.
    /// </summary>
    [Theory]
    [InlineData(GradeReach.Exactly)]
    [InlineData(GradeReach.MayUseAHigherGrade)]
    public void Package_sizes_still_overshoot_because_packages_cannot_be_split(GradeReach reach)
    {
        GradePlan plan = Plan(
            QualityTier.Standard,
            6,
            reach,
            out PackageFill fill,
            new CarriedLot(QualityTier.Standard, Jar, 2));

        Assert.True(plan.CanDeliver);
        Assert.Equal(QualityTier.Standard, plan.ChosenQuality);
        Assert.Equal(10, fill.Units);
    }

    /// <summary>
    /// It is one bound on the existing walk rather than a second search: wherever
    /// the contract's own grade covers the order, the two answers produce the
    /// same delivery, package for package.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(21)]
    public void Where_the_grade_ordered_covers_it_the_two_answers_agree(int order)
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Baggie, 2),
            new CarriedLot(QualityTier.Standard, Jar, 1),
            new CarriedLot(QualityTier.Standard, Brick, 1),
            new CarriedLot(QualityTier.Heavenly, Brick, 1),
        };

        GradeFill exactly = PackagePlan.Choose(bag, QualityTier.Standard, order, GradeReach.Exactly);
        GradeFill roundedUp = PackagePlan.Choose(
            bag, QualityTier.Standard, order, GradeReach.MayUseAHigherGrade);

        Assert.True(exactly.Covers);
        Assert.Equal(roundedUp.Quality, exactly.Quality);
        Assert.Equal(roundedUp.Units, exactly.Units);
        Assert.Equal(roundedUp.Packages, exactly.Packages);
    }

    /// <summary>
    /// A grade below the contract's is no more an answer in exact mode than it
    /// ever was. The bound only stops the walk upward; handing over something
    /// worse stays a decision for a player.
    /// </summary>
    [Fact]
    public void Nothing_worse_than_the_grade_ordered_is_ever_taken()
    {
        GradePlan plan = Plan(
            QualityTier.Premium,
            5,
            GradeReach.Exactly,
            out PackageFill fill,
            new CarriedLot(QualityTier.Poor, Brick, 1));

        Assert.False(plan.CanDeliver);
        Assert.Equal(0, fill.Units);
        Assert.Contains("nothing in reach is that good", plan.Reason);
    }

    /// <summary>
    /// And the ordinary shortages keep their own words. The contract's grade is
    /// in the bag and there is not enough of it: the setting is not what stopped
    /// this one, so the sentence must not claim it was.
    /// </summary>
    [Fact]
    public void A_shortage_of_the_grade_ordered_is_still_said_as_a_shortage()
    {
        GradePlan plan = Plan(
            QualityTier.Standard,
            20,
            GradeReach.Exactly,
            out PackageFill fill,
            new CarriedLot(QualityTier.Standard, Jar, 3));

        Assert.False(plan.CanDeliver);
        Assert.Equal(15, fill.Carrying);
        Assert.Contains("no single grade covers", plan.Reason);
        Assert.DoesNotContain("stays in the bag", plan.Reason);
    }

    /// <summary>
    /// Story 23: a fresh install delivers exactly the grade that was ordered.
    /// The preferences file is seeded from this object, so this is the default
    /// in the file and in the app at once.
    /// </summary>
    [Fact]
    public void A_fresh_install_delivers_exactly_the_grade_that_was_ordered()
    {
        Assert.False(new AdvisorSettings().MayUseAHigherGrade);
        Assert.Equal(GradeReach.Exactly, new AdvisorSettings().Reach);
        Assert.Equal(
            GradeReach.MayUseAHigherGrade,
            new AdvisorSettings { MayUseAHigherGrade = true }.Reach);
    }

    private static GradePlan Plan(
        int requestedQuality,
        int requestedQuantity,
        GradeReach reach,
        out PackageFill fill,
        params CarriedLot[] lots) =>
        GradeChoice.Plan(
            new DeliveryRequest("ogkush", requestedQuality, requestedQuantity, 950f, QualityTier.Poor),
            lots,
            reach,
            out fill);
}
