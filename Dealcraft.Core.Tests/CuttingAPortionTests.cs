using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Opening one package that holds more than the order, and where that decision
/// has to be made.
/// </summary>
/// <remarks>
/// <para>
/// The first attempt put cutting into the moment the goods are taken out of the
/// inventory, and it could never run: the handover asks whether the order can
/// be covered <em>before</em> it claims the contract, and a plan that says no
/// ends the matter there. The owner found it by asking why restocking had
/// always worked — it works because a plan that cannot deliver never claims,
/// which is the same gate.
/// </para>
/// <para>
/// So these hold the decision where it is made. A brick against an order of six
/// is the case he described.
/// </para>
/// </remarks>
public class CuttingAPortionTests
{
    private const int Baggie = 1;
    private const int Jar = 5;
    private const int Brick = 20;

    /// <summary>
    /// A brick covers an order of six perfectly well — and sends twenty units
    /// to do it. That overshoot is the thing being fixed, not a shortage.
    /// </summary>
    [Fact]
    public void Without_leave_to_cut_a_brick_overshoots_an_order_of_six()
    {
        PackageFill fill = PackagePlan.Fill(new[] { new PackageLot(Brick, 1) }, 6);

        Assert.True(fill.Covers);
        Assert.Equal(20, fill.Units);
    }

    [Fact]
    public void With_leave_to_cut_it_covers_exactly_six()
    {
        PackageFill fill = PackagePlan.Fill(new[] { new PackageLot(Brick, 1) }, 6, mayCut: true);

        Assert.True(fill.Covers);
        Assert.Equal(6, fill.Units);
        Assert.Equal(1, fill.Positions);
    }

    /// <summary>
    /// The smallest package that covers it is the one opened, so a brick is
    /// left whole while a jar will do.
    /// </summary>
    [Fact]
    public void The_smallest_package_that_covers_the_order_is_the_one_opened()
    {
        var bag = new[] { new PackageLot(Jar, 1), new PackageLot(Brick, 1) };

        PackageFill fill = PackagePlan.Fill(bag, 3, mayCut: true);

        Assert.True(fill.Covers);
        Assert.Equal(1, fill.From(0));
        Assert.Equal(0, fill.From(1));
    }

    /// <summary>
    /// And packing as the game allows still wins: switching cutting on changes
    /// nothing about a bag that could already cover the order.
    /// </summary>
    [Fact]
    public void An_order_that_packs_the_ordinary_way_is_not_cut()
    {
        var bag = new[] { new PackageLot(Baggie, 20) };

        PackageFill plain = PackagePlan.Fill(bag, 6);
        PackageFill cutting = PackagePlan.Fill(bag, 6, mayCut: true);

        Assert.True(plain.Covers);
        Assert.Equal(plain.Units, cutting.Units);
        Assert.Equal(plain.Packages, cutting.Packages);
    }

    /// <summary>
    /// The whole point, at the seam the handover actually asks: a plan that
    /// could not deliver now can.
    /// </summary>
    [Fact]
    public void The_plan_sends_exactly_the_order_once_cutting_is_allowed()
    {
        var request = new DeliveryRequest("ogkush", QualityTier.Standard, 6, 600f, QualityTier.Poor);
        var bag = new[] { new CarriedLot(QualityTier.Standard, Brick, 1) };

        GradeChoice.Plan(request, bag, GradeReach.Exactly, out PackageFill plain);
        GradePlan with = GradeChoice.Plan(
            request, bag, GradeReach.Exactly, mayCut: true, out PackageFill fill);

        // Both deliver. What changes is how much leaves the bag.
        Assert.Equal(20, plain.Units);
        Assert.True(with.CanDeliver);
        Assert.Equal(6, fill.Units);
    }
}
