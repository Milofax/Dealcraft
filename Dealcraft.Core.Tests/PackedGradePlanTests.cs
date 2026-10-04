using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The plan the handover pass actually makes, now that the grade and the
/// packaging are one question. Everything the units-only plan says still holds;
/// what changes is that the grade it names is one the bag can hand over.
/// </summary>
public class PackedGradePlanTests
{
    private const int Baggie = 1;
    private const int Jar = 5;
    private const int Brick = 20;

    /// <summary>
    /// What these cases are about: the plan as it is made when the player lets
    /// it round the grade up. Exact mode is <see cref="ExactlyTheGradeOrderedTests"/>.
    /// </summary>
    private const GradeReach RoundingUp = GradeReach.MayUseAHigherGrade;

    private static GradePlan Plan(
        int requestedQuality,
        int requestedQuantity,
        out PackageFill fill,
        params CarriedLot[] lots) =>
        GradeChoice.Plan(
            new DeliveryRequest("ogkush", requestedQuality, requestedQuantity, 950f, QualityTier.Poor),
            lots,
            RoundingUp,
            out fill);

    /// <summary>
    /// The contract's own grade, packaged, and no stock spent on a tier the
    /// customer did not order.
    /// </summary>
    [Fact]
    public void Takes_the_contracts_own_grade_where_the_packages_reach_it()
    {
        GradePlan plan = Plan(
            QualityTier.Standard,
            10,
            out PackageFill fill,
            new CarriedLot(QualityTier.Standard, Jar, 2),
            new CarriedLot(QualityTier.Heavenly, Brick, 1));

        Assert.True(plan.CanDeliver);
        Assert.Equal(QualityTier.Standard, plan.ChosenQuality);
        Assert.Equal(10, plan.ChosenUnits);
        Assert.Equal(10, fill.Units);
        Assert.Equal(2, fill.Packages);
        Assert.Contains("what the contract asks for", plan.Reason);
    }

    /// <summary>
    /// <b>The defect this overload exists for.</b> Five baggies of Standard hold
    /// the units for an order of five and a handover holds four packages, so the
    /// units-only plan names Standard and the delivery is then refused — while a
    /// jar of Heavenly that covers the order exactly sits in the same pocket.
    /// </summary>
    [Fact]
    public void A_grade_it_cannot_package_does_not_cost_the_contract()
    {
        // Standard's five baggies are in five separate slots, which is five of
        // the screen's four positions. One slot holding five would go across as
        // one position and there would be nothing to choose between.
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Baggie, 1),
            new CarriedLot(QualityTier.Standard, Baggie, 1),
            new CarriedLot(QualityTier.Standard, Baggie, 1),
            new CarriedLot(QualityTier.Standard, Baggie, 1),
            new CarriedLot(QualityTier.Standard, Baggie, 1),
            new CarriedLot(QualityTier.Heavenly, Jar, 1),
        };

        var request = new DeliveryRequest("ogkush", QualityTier.Standard, 5, 950f, QualityTier.Poor);

        // What shipped: units per grade, Standard has five, and the packaging is
        // asked afterwards and refuses.
        GradePlan byUnits = GradeChoice.Plan(
            request,
            new[] { new GradeStock(QualityTier.Standard, 5), new GradeStock(QualityTier.Heavenly, 5) });

        Assert.Equal(QualityTier.Standard, byUnits.ChosenQuality);
        Assert.False(
            PackagePlan.Fill(
                new[]
                {
                    new PackageLot(Baggie, 1),
                    new PackageLot(Baggie, 1),
                    new PackageLot(Baggie, 1),
                    new PackageLot(Baggie, 1),
                    new PackageLot(Baggie, 1),
                },
                5).Covers);

        // Asked as one question, the contract is covered.
        GradePlan plan = GradeChoice.Plan(request, bag, RoundingUp, out PackageFill fill);

        Assert.True(plan.CanDeliver);
        Assert.Equal(QualityTier.Heavenly, plan.ChosenQuality);
        Assert.True(fill.Covers);
        Assert.Equal(5, fill.Units);
        Assert.Contains("whose packages reach", plan.Reason);
    }

    /// <summary>
    /// Where nothing can package the order, the refusal says which of the two
    /// problems it was: the product is there and it is in too many packages.
    /// </summary>
    [Fact]
    public void Too_many_packages_is_said_as_itself()
    {
        GradePlan plan = Plan(
            QualityTier.Standard,
            5,
            out PackageFill fill,
            new CarriedLot(QualityTier.Standard, Baggie, 1),
            new CarriedLot(QualityTier.Standard, Baggie, 1),
            new CarriedLot(QualityTier.Standard, Baggie, 1),
            new CarriedLot(QualityTier.Standard, Baggie, 1),
            new CarriedLot(QualityTier.Standard, Baggie, 1));

        Assert.False(plan.CanDeliver);
        Assert.Equal(PackageFillOutcome.NeedsMorePackages, fill.Outcome);
        Assert.Contains("5 units in reach", plan.Reason);
        Assert.Contains("fewer, larger packages", plan.Reason);
    }

    /// <summary>
    /// And where the product is simply not there, the reason is the one it
    /// always was — the owner's <i>"der will 20, ich habe aber nur 18"</i>.
    /// </summary>
    [Fact]
    public void Not_enough_product_is_still_said_as_itself()
    {
        GradePlan plan = Plan(
            QualityTier.Standard,
            20,
            out PackageFill fill,
            new CarriedLot(QualityTier.Standard, Jar, 3),
            new CarriedLot(QualityTier.Standard, Baggie, 3));

        Assert.False(plan.CanDeliver);
        Assert.Equal(PackageFillOutcome.NotEnoughCarried, fill.Outcome);
        Assert.Equal(18, fill.Carrying);
        Assert.Contains("no single grade covers", plan.Reason);

        // What was short, and by how much. This used to be asserted through the
        // contract row's sentence; the row is gone and the fill is the thing the
        // test was about either way.
        Assert.Equal(2, 20 - fill.Carrying);
    }

    /// <summary>
    /// Nothing in reach that meets the grade is a different refusal again, and
    /// handing over a lower grade stays a decision for a player.
    /// </summary>
    [Fact]
    public void Nothing_good_enough_is_not_answered_with_something_worse()
    {
        GradePlan plan = Plan(
            QualityTier.Premium,
            5,
            out PackageFill fill,
            new CarriedLot(QualityTier.Poor, Brick, 1));

        Assert.False(plan.CanDeliver);
        Assert.False(fill.Covers);
        Assert.Contains("nothing in reach is that good", plan.Reason);
    }

    /// <summary>
    /// The lines the log and the app read are unchanged: what is going over, and
    /// what every other grade in reach would have paid.
    /// </summary>
    [Fact]
    public void It_still_says_what_the_other_grades_would_have_paid()
    {
        GradePlan plan = Plan(
            QualityTier.Poor,
            5,
            out PackageFill _,
            new CarriedLot(QualityTier.Poor, Jar, 1),
            new CarriedLot(QualityTier.Heavenly, Jar, 1));

        Assert.Equal(QualityTier.Poor, plan.ChosenQuality);

        IReadOnlyList<string> lines = GradeChoice.Describe(plan, amount => amount.ToString("0.00"));

        Assert.Contains(lines, line => line.Contains("Handing over 5 x Poor"));

        // Three tiers on a payment of 950 is 427.50, uncapped, as the evening's
        // ledger settled it.
        Assert.Contains(lines, line => line.Contains("Heavenly") && line.Contains("427.50"));
    }

    /// <summary>An empty bag delivers nothing and says why.</summary>
    [Fact]
    public void An_empty_bag_delivers_nothing()
    {
        GradePlan plan = Plan(QualityTier.Standard, 5, out PackageFill fill);

        Assert.False(plan.CanDeliver);
        Assert.Equal(0, fill.Units);
        Assert.Contains("there is none in reach", plan.Reason);
    }

    [Fact]
    public void A_plan_without_a_bag_is_a_programming_error()
    {
        var request = new DeliveryRequest("ogkush", QualityTier.Standard, 5, 100f, QualityTier.Poor);

        Assert.Throws<ArgumentNullException>(
            () => GradeChoice.Plan(
                request, (IReadOnlyList<CarriedLot>)null!, RoundingUp, out PackageFill _));
    }
}
