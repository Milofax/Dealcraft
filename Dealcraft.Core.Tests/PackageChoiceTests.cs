using System.Collections.Generic;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The grade and the packaging as one question. The owner put it that way —
/// <i>"Qualitätsstufe, aber auch Menge zu vorhandenen Verpackungen"</i> — and
/// deciding them in turn refuses contracts the bag could fill.
/// </summary>
public class PackageChoiceTests
{
    private const int Baggie = 1;
    private const int Jar = 5;
    private const int Brick = 20;

    /// <summary>
    /// The walk upward, which is what every case here is about: the lowest grade
    /// at or above the contract's that the packages can reach. The answer that
    /// stops the walk is <see cref="ExactlyTheGradeOrderedTests"/>.
    /// </summary>
    private const GradeReach RoundingUp = GradeReach.MayUseAHigherGrade;

    /// <summary>
    /// The case that made the joint choice necessary. Five baggies of Standard
    /// hold the units for an order of five Standard and cannot deliver them: a
    /// handover holds four packages. A jar of Heavenly in the same bag covers it
    /// exactly, and deciding the grade first never looks at it.
    /// </summary>
    [Fact]
    public void A_grade_whose_packages_cannot_reach_the_order_is_not_chosen()
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Baggie, 5),
            new CarriedLot(QualityTier.Heavenly, Jar, 1),
        };

        GradeFill choice = PackagePlan.Choose(bag, QualityTier.Standard, 5, RoundingUp);

        Assert.True(choice.Covers);
        Assert.Equal(QualityTier.Heavenly, choice.Quality);
        Assert.Equal(5, choice.Units);
        Assert.Equal(1, choice.Packages);
    }

    /// <summary>
    /// And where the contract's own grade can be packaged, it is taken and
    /// nothing better is spent: the rule that ships is the lowest adequate
    /// grade, not the largest bonus.
    /// </summary>
    [Fact]
    public void The_lowest_grade_that_can_be_packaged_is_the_one_chosen()
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Jar, 1),
            new CarriedLot(QualityTier.Heavenly, Jar, 1),
        };

        GradeFill choice = PackagePlan.Choose(bag, QualityTier.Standard, 4, RoundingUp);

        Assert.Equal(QualityTier.Standard, choice.Quality);
        Assert.Equal(5, choice.Units);
    }

    /// <summary>
    /// It does not spend a better grade to give away fewer units. Four baggies
    /// of Heavenly against an order of four is not an improvement on a jar of
    /// Standard; it is four units of Heavenly gone.
    /// </summary>
    [Fact]
    public void A_better_grade_is_not_spent_to_save_a_unit()
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Jar, 1),
            new CarriedLot(QualityTier.Heavenly, Baggie, 4),
        };

        GradeFill choice = PackagePlan.Choose(bag, QualityTier.Standard, 4, RoundingUp);

        Assert.Equal(QualityTier.Standard, choice.Quality);
        Assert.Equal(5, choice.Units);
    }

    /// <summary>A grade below the contract's is never an answer to it.</summary>
    [Fact]
    public void A_grade_below_the_contract_is_never_chosen()
    {
        var bag = new[] { new CarriedLot(QualityTier.Poor, Brick, 1) };

        GradeFill choice = PackagePlan.Choose(bag, QualityTier.Premium, 5, RoundingUp);

        Assert.False(choice.Covers);
        Assert.Equal(0, choice.Fill.Carrying);
    }

    /// <summary>
    /// Where nothing covers the order, the grade reported is the adequate one
    /// there is most of — the one worth fetching more of — and the fill says
    /// which of the two problems it was.
    /// </summary>
    [Fact]
    public void Where_nothing_covers_it_the_grade_with_the_most_in_reach_is_named()
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Jar, 3),
            new CarriedLot(QualityTier.Heavenly, Baggie, 1),
        };

        GradeFill choice = PackagePlan.Choose(bag, QualityTier.Standard, 20, RoundingUp);

        Assert.False(choice.Covers);
        Assert.Equal(QualityTier.Standard, choice.Quality);
        Assert.Equal(15, choice.Fill.Carrying);
        Assert.Equal(PackageFillOutcome.NotEnoughCarried, choice.Fill.Outcome);
    }

    /// <summary>
    /// Product enough and packages too many is the other problem, and the
    /// difference is the errand: twenty-five baggies against an order of twenty
    /// needs a brick, not more product.
    /// </summary>
    [Fact]
    public void Enough_product_in_too_many_packages_is_its_own_answer()
    {
        var bag = new[] { new CarriedLot(QualityTier.Standard, Baggie, 25) };

        GradeFill choice = PackagePlan.Choose(bag, QualityTier.Standard, 20, RoundingUp);

        Assert.False(choice.Covers);
        Assert.Equal(PackageFillOutcome.NeedsMorePackages, choice.Fill.Outcome);
        Assert.Equal(25, choice.Fill.Carrying);
    }

    /// <summary>
    /// The joint choice agrees with the single-contract planner wherever the
    /// packaging is not in the way: one grade, and the least product of it that
    /// covers the order.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(8, 20)]
    [InlineData(21, 21)]
    [InlineData(25, 25)]
    public void It_takes_the_same_least_product_the_planner_takes(int order, int least)
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Baggie, 2),
            new CarriedLot(QualityTier.Standard, Jar, 1),
            new CarriedLot(QualityTier.Standard, Brick, 1),
        };

        GradeFill choice = PackagePlan.Choose(bag, QualityTier.Standard, order, RoundingUp);

        Assert.True(choice.Covers);
        Assert.Equal(least, choice.Units);
    }

    /// <summary>
    /// The options an allocation weighs are the minimal covers and nothing
    /// else: two jars against an order of four is one jar, and never two.
    /// </summary>
    [Fact]
    public void Options_are_the_covers_that_waste_no_package()
    {
        var bag = new[] { new CarriedLot(QualityTier.Standard, Jar, 2) };

        IReadOnlyList<PackageFill> four = PackagePlan.Options(
            bag, QualityTier.Standard, 4, RoundingUp);
        IReadOnlyList<PackageFill> six = PackagePlan.Options(
            bag, QualityTier.Standard, 6, RoundingUp);

        PackageFill jar = Assert.Single(four);
        Assert.Equal(5, jar.Units);
        Assert.Equal(1, jar.Packages);

        PackageFill both = Assert.Single(six);
        Assert.Equal(10, both.Units);
        Assert.Equal(2, both.Packages);
    }

    /// <summary>
    /// Where two different combinations both cover the order without waste,
    /// both are offered — that is the whole point of the list, since another
    /// contract may want one of them.
    /// </summary>
    [Fact]
    public void Two_ways_of_covering_the_order_are_both_offered()
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Jar, 1),
            new CarriedLot(QualityTier.Standard, Baggie, 4),
        };

        IReadOnlyList<PackageFill> options = PackagePlan.Options(
            bag, QualityTier.Standard, 4, RoundingUp);

        Assert.Equal(2, options.Count);
        Assert.Contains(options, option => option.Units == 5 && option.Packages == 1);
        Assert.Contains(options, option => option.Units == 4 && option.Packages == 4);
    }

    /// <summary>
    /// One option draws on one grade. Splitting a delivery across grades changes
    /// the mean quality difference the game pays its bonus on into something
    /// nobody chose, so no option ever mixes them.
    /// </summary>
    [Fact]
    public void No_option_mixes_two_grades()
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Jar, 1),
            new CarriedLot(QualityTier.Heavenly, Jar, 1),
        };

        foreach (PackageFill option in PackagePlan.Options(
                     bag, QualityTier.Standard, 6, RoundingUp))
        {
            Assert.True(option.From(0) == 0 || option.From(1) == 0);
        }
    }

    /// <summary>
    /// An order no grade can cover has no options at all, which is how a
    /// contract comes to be short rather than served badly.
    /// </summary>
    [Fact]
    public void An_order_nothing_covers_has_no_options()
    {
        var bag = new[] { new CarriedLot(QualityTier.Standard, Baggie, 18) };

        Assert.Empty(PackagePlan.Options(bag, QualityTier.Standard, 20, RoundingUp));
    }

    /// <summary>
    /// No option holds more packages than a handover does, whatever the bag: the
    /// mod does not do what a player could not do by hand.
    /// </summary>
    [Fact]
    public void No_option_holds_more_packages_than_a_handover()
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Standard, Baggie, 9),
            new CarriedLot(QualityTier.Standard, Jar, 9),
        };

        foreach (PackageFill option in PackagePlan.Options(
                     bag, QualityTier.Standard, 9, RoundingUp))
        {
            Assert.InRange(option.Packages, 1, PackagePlan.MostPackages);
        }
    }
}
