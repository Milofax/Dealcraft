using System.Collections.Generic;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// An order no single package holds more than: whole packages for most of it,
/// and one package opened for what is left.
/// </summary>
/// <remarks>
/// The owner's decision, against an order of six and two jars of five: the
/// negotiated quantity is never rounded up, it is met at the handover. One whole
/// jar, one unit cut out of the other, and the other four back in the bag.
/// </remarks>
public class CuttingTheRemainderTests
{
    private const int Baggie = 1;
    private const int Jar = 5;
    private const int Brick = 20;

    /// <summary>Packages one inventory slot holds.</summary>
    private const int Slot = 20;

    private static readonly int[] Packagings = { Baggie, Jar, Brick };

    /// <summary>
    /// Nothing is created and nothing is lost, said in the terms the inventory
    /// writes in: each slot loses what the fill says leaves it, the portion
    /// crosses, and what is left of the opened package comes back.
    /// </summary>
    private static void Conserved(PackageLot[] bag, int order, PackageFill fill, PortionSplit split)
    {
        int before = 0;
        int after = 0;

        for (int lot = 0; lot < bag.Length; lot++)
        {
            Assert.InRange(fill.From(lot), 0, bag[lot].Packages);
            before += bag[lot].Units;
            after += (bag[lot].Packages - fill.From(lot)) * bag[lot].UnitsPerPackage;
        }

        foreach (PortionStack stack in split.Leftover)
        {
            after += stack.Units;
        }

        Assert.Equal(order, split.PortionUnits * split.Portions + WholeUnits(bag, fill));
        Assert.Equal(before, after + order);
    }

    private static int WholeUnits(PackageLot[] bag, PackageFill fill)
    {
        int units = 0;

        for (int lot = 0; lot < bag.Length; lot++)
        {
            units += fill.Whole(lot) * bag[lot].UnitsPerPackage;
        }

        return units;
    }

    private static PortionSplit Split(PackageLot[] bag, PackageFill fill) =>
        PortionCut.Split(fill, bag, Packagings, Slot, roomBeside: null);

    /// <summary>
    /// <b>The trap.</b> Both jars are in one slot, so the whole part and the cut
    /// come out of it together: the slot is written once, with two, and it empties
    /// — so the remainder has that slot and needs no other.
    /// </summary>
    [Fact]
    public void Six_from_two_jars_in_one_slot()
    {
        var bag = new[] { new PackageLot(Jar, 2) };

        PackageFill fill = PackagePlan.Fill(bag, 6, mayCut: true);
        PortionSplit split = Split(bag, fill);

        Assert.True(fill.Cuts);
        Assert.Equal(6, fill.Units);
        Assert.Equal(0, fill.Opened);
        Assert.Equal(1, fill.Portion);
        Assert.Equal(1, fill.Whole(0));

        // Two leave the slot, in one figure.
        Assert.Equal(2, fill.From(0));
        Assert.Equal(2, fill.Positions);

        Assert.True(split.Possible);
        Assert.Equal(0, split.SlotsNeeded);
        Conserved(bag, 6, fill, split);
    }

    /// <summary>
    /// A third jar stays behind, so the slot does not empty and the remainder
    /// needs a place of its own. The only thing that changed is the count.
    /// </summary>
    [Fact]
    public void A_jar_left_in_the_slot_keeps_the_remainder_out_of_it()
    {
        var bag = new[] { new PackageLot(Jar, 3) };

        PackageFill fill = PackagePlan.Fill(bag, 6, mayCut: true);
        PortionSplit split = Split(bag, fill);

        Assert.Equal(2, fill.From(0));
        Assert.Equal(1, split.SlotsNeeded);
        Conserved(bag, 6, fill, split);
    }

    [Fact]
    public void Six_from_two_jars_in_two_slots()
    {
        var bag = new[] { new PackageLot(Jar, 1), new PackageLot(Jar, 1) };

        PackageFill fill = PackagePlan.Fill(bag, 6, mayCut: true);
        PortionSplit split = Split(bag, fill);

        Assert.True(fill.Cuts);
        Assert.Equal(6, fill.Units);
        Assert.Equal(1, fill.From(0));
        Assert.Equal(1, fill.From(1));
        Assert.Equal(1, fill.Whole(0) + fill.Whole(1));
        Assert.Equal(2, fill.Positions);
        Assert.Equal(0, split.SlotsNeeded);
        Conserved(bag, 6, fill, split);
    }

    /// <summary>
    /// Between two slots of the same jar, the one the cut empties is opened: a
    /// remainder that has a slot of its own is one that goes out even with the
    /// bag full.
    /// </summary>
    [Fact]
    public void The_lot_that_empties_is_the_one_opened()
    {
        var bag = new[] { new PackageLot(Jar, 3), new PackageLot(Jar, 1) };

        PackageFill fill = PackagePlan.Fill(bag, 6, mayCut: true);

        Assert.Equal(1, fill.Opened);
        Assert.Equal(0, Split(bag, fill).SlotsNeeded);
    }

    [Fact]
    public void Eleven_from_three_jars()
    {
        var bag = new[] { new PackageLot(Jar, 3) };

        PackageFill fill = PackagePlan.Fill(bag, 11, mayCut: true);
        PortionSplit split = Split(bag, fill);

        Assert.Equal(11, fill.Units);
        Assert.Equal(2, fill.Whole(0));
        Assert.Equal(1, fill.Portion);
        Assert.Equal(3, fill.From(0));
        Conserved(bag, 11, fill, split);
    }

    [Fact]
    public void Twenty_one_from_two_bricks()
    {
        var bag = new[] { new PackageLot(Brick, 2) };

        PackageFill fill = PackagePlan.Fill(bag, 21, mayCut: true);
        PortionSplit split = Split(bag, fill);

        Assert.Equal(21, fill.Units);
        Assert.Equal(1, fill.Whole(0));
        Assert.Equal(1, fill.Portion);
        Assert.Equal(19, split.Leftover[0].Units);
        Conserved(bag, 21, fill, split);
    }

    /// <summary>
    /// Unchanged, and on purpose: a brick holds more than the order, so it is the
    /// one package opened and the jar stays in the bag. The order is the same six
    /// units either way, and this is what a single package holding more has
    /// always done.
    /// </summary>
    [Fact]
    public void Six_from_a_jar_and_a_brick_opens_the_brick_alone()
    {
        var bag = new[] { new PackageLot(Jar, 1), new PackageLot(Brick, 1) };

        PackageFill fill = PackagePlan.Fill(bag, 6, mayCut: true);

        Assert.Equal(6, fill.Units);
        Assert.Equal(1, fill.Opened);
        Assert.Equal(6, fill.Portion);
        Assert.Equal(0, fill.From(0));
        Assert.Equal(1, fill.From(1));
        Assert.Equal(1, fill.Positions);
    }

    /// <summary>
    /// The plan says the cut is worth making; the packaging says whether it can
    /// be. Packagings of three and seven cannot make one unit, so the split says
    /// no — and that answer is what sends the whole packages instead.
    /// </summary>
    [Fact]
    public void A_portion_no_packaging_can_hold_is_no_cut()
    {
        var bag = new[] { new PackageLot(Jar, 2) };

        PackageFill fill = PackagePlan.Fill(bag, 6, mayCut: true);
        PortionSplit split = PortionCut.Split(fill, bag, new[] { 3, 7 }, Slot, roomBeside: null);

        Assert.True(fill.Cuts);
        Assert.False(split.Possible);

        // What the taking falls back on, and it overshoots exactly as before.
        Assert.Equal(10, PackagePlan.Fill(bag, 6).Units);
    }

    /// <summary>
    /// A cut takes a position of its own. Three baggies in three slots and a jar
    /// reach eight units in four positions, which leaves none for the portion, so
    /// the whole part drops to what still fits and the portion grows to match.
    /// </summary>
    [Fact]
    public void The_whole_part_gives_way_to_the_position_limit()
    {
        var bag = new[]
        {
            new PackageLot(Baggie, 1),
            new PackageLot(Baggie, 1),
            new PackageLot(Baggie, 1),
            new PackageLot(Jar, 2),
        };

        PackageFill fill = PackagePlan.Fill(bag, 9, mayCut: true);

        Assert.True(fill.Cuts);
        Assert.Equal(9, fill.Units);
        Assert.Equal(2, fill.Portion);
        Assert.Equal(PackagePlan.MostPositions, fill.Positions);
    }

    /// <summary>
    /// Four slots of two jars against thirty-six. The largest whole part is seven
    /// jars in four positions and the cut would be a fifth; six jars leaves six
    /// units, which no jar holds. So no cut, and the eight jars go.
    /// </summary>
    [Fact]
    public void With_no_position_left_for_the_portion_the_whole_packages_go()
    {
        var bag = new[]
        {
            new PackageLot(Jar, 2),
            new PackageLot(Jar, 2),
            new PackageLot(Jar, 2),
            new PackageLot(Jar, 2),
        };

        PackageFill fill = PackagePlan.Fill(bag, 36, mayCut: true);

        Assert.True(fill.Covers);
        Assert.False(fill.Cuts);
        Assert.Equal(40, fill.Units);
        Assert.Equal(-1, fill.Opened);
    }

    [Theory]
    [InlineData(6, 2, 0)]
    [InlineData(11, 3, 0)]
    [InlineData(21, 0, 2)]
    public void With_cutting_off_nothing_changes(int order, int jars, int bricks)
    {
        var bag = new[] { new PackageLot(Jar, jars), new PackageLot(Brick, bricks) };

        PackageFill fill = PackagePlan.Fill(bag, order);

        Assert.False(fill.Cuts);
        Assert.Equal(-1, fill.Opened);
        Assert.Equal(0, fill.Portion);
        Assert.Equal(fill.From(0), fill.Whole(0));
        Assert.True(fill.Units > order);
    }

    /// <summary>
    /// <b>The grade path has to carry the cut across.</b> It rebuilds the fill in
    /// the whole bag's index space and used to leave the cut behind, so the
    /// question the handover asks before it claims a contract was never asked. A
    /// lot of another grade in front of the jars is what proves the index moved.
    /// </summary>
    [Fact]
    public void The_grade_path_keeps_the_cut_and_says_it_in_the_whole_bag()
    {
        var bag = new[]
        {
            new CarriedLot(QualityTier.Poor, Brick, 1),
            new CarriedLot(QualityTier.Standard, Jar, 2),
        };

        GradeFill choice = PackagePlan.Choose(
            bag, QualityTier.Standard, 6, GradeReach.Exactly, mayCut: true);

        Assert.True(choice.Covers);
        Assert.True(choice.Fill.Cuts);
        Assert.Equal(1, choice.Fill.Opened);
        Assert.Equal(1, choice.Fill.Portion);
        Assert.Equal(2, choice.Fill.From(1));
        Assert.Equal(1, choice.Fill.Whole(1));
        Assert.Equal(0, choice.Fill.From(0));
        Assert.Equal(6, choice.Units);
    }

    /// <summary>
    /// The plan the handover reads before it claims, at the seam it reads it.
    /// </summary>
    [Fact]
    public void The_plan_sends_exactly_six_out_of_two_jars()
    {
        var request = new DeliveryRequest("ogkush", QualityTier.Standard, 6, 600f, QualityTier.Poor);
        var bag = new[] { new CarriedLot(QualityTier.Standard, Jar, 2) };

        GradePlan plan = GradeChoice.Plan(
            request, bag, GradeReach.Exactly, mayCut: true, out PackageFill fill);

        Assert.True(plan.CanDeliver);
        Assert.True(fill.Cuts);
        Assert.Equal(6, fill.Units);
    }

    /// <summary>
    /// <b>A cut that fails halfway loses nothing.</b> Stop it after any number of
    /// writes: what is in the bag plus what is out of it is what there was, so
    /// putting the goods that are out back restores the bag exactly. Two jars in
    /// two slots with the rest going to a third is the case that used to lose all
    /// ten units.
    /// </summary>
    [Theory]
    [InlineData(6, -1, 1, 1)]
    [InlineData(6, 0, 1, 1)]
    [InlineData(6, -1, 2, 0)]
    [InlineData(6, 0, 2, 0)]
    [InlineData(6, -1, 3, 0)]
    [InlineData(11, 0, 3, 0)]
    public void Stopped_after_any_write_every_unit_is_in_the_bag_or_out(
        int order, int back, int first, int second)
    {
        var bag = new[] { new PackageLot(Jar, first), new PackageLot(Jar, second) };
        PackageFill fill = PackagePlan.Fill(bag, order, mayCut: true);
        PortionSplit split = Split(bag, fill);

        IReadOnlyList<CutWrite> writes = PortionCut.Writes(fill, split.Leftover.Count, back);

        for (int landed = 0; landed <= writes.Count; landed++)
        {
            int inBag = bag[0].Units + bag[1].Units;

            for (int at = 0; at < landed; at++)
            {
                if (writes[at].Lot >= 0)
                {
                    inBag -= writes[at].Packages * bag[writes[at].Lot].UnitsPerPackage;
                }

                if (writes[at].Stack >= 0)
                {
                    inBag += split.Leftover[writes[at].Stack].Units;
                }
            }

            CutGoods goods = PortionCut.Out(writes, landed, fill);
            int outOfBag = goods.Portion ? fill.Portion : 0;

            foreach (int lot in goods.WholeLots)
            {
                outOfBag += fill.Whole(lot) * bag[lot].UnitsPerPackage;
            }

            foreach (int stack in goods.Stacks)
            {
                outOfBag += split.Leftover[stack].Units;
            }

            Assert.Equal(bag[0].Units + bag[1].Units, inBag + outOfBag);
        }
    }

    /// <summary>
    /// Nothing goes back before it left, so a failure is never a surplus. And all
    /// the writes done, what is out is what the customer gets.
    /// </summary>
    [Fact]
    public void The_slots_give_up_their_packages_before_the_remainder_goes_back()
    {
        var bag = new[] { new PackageLot(Jar, 1), new PackageLot(Jar, 1) };
        PackageFill fill = PackagePlan.Fill(bag, 6, mayCut: true);

        IReadOnlyList<CutWrite> writes = PortionCut.Writes(fill, 1, back: -1);

        Assert.Equal(3, writes.Count);
        Assert.True(writes[0].Lot >= 0);
        Assert.True(writes[1].Lot >= 0);
        Assert.Equal(-1, writes[2].Lot);
        Assert.Equal(0, writes[2].Stack);

        CutGoods done = PortionCut.Out(writes, writes.Count, fill);

        Assert.True(done.Portion);
        Assert.Single(done.WholeLots);
        Assert.Empty(done.Stacks);
    }

    /// <summary>
    /// <b>Replacing clears first, so it can stop halfway.</b> Two jars in one slot,
    /// the slot emptied and the remainder never stored: all ten units are out, and
    /// counting that write as landed or as not landed would lose some of them.
    /// </summary>
    [Fact]
    public void A_replacing_write_that_stops_halfway_leaves_everything_out()
    {
        var bag = new[] { new PackageLot(Jar, 2) };
        PackageFill fill = PackagePlan.Fill(bag, 6, mayCut: true);
        PortionSplit split = Split(bag, fill);

        IReadOnlyList<CutWrite> writes = PortionCut.Writes(fill, 1, back: 0);
        CutGoods goods = PortionCut.Out(writes, 1, fill, emptiedOnly: true);

        Assert.True(goods.Portion);
        Assert.Equal(new[] { 0 }, goods.WholeLots);
        Assert.Equal(new[] { 0 }, goods.Stacks);
        Assert.Equal(
            bag[0].Units,
            fill.Whole(0) * Jar + fill.Portion + split.Leftover[0].Units);
    }

    /// <summary>
    /// The remainder going back into its own slot is one write with the packages
    /// leaving it, so that slot is never written twice.
    /// </summary>
    [Fact]
    public void The_slot_that_takes_the_remainder_back_is_written_once()
    {
        var bag = new[] { new PackageLot(Jar, 2) };
        PackageFill fill = PackagePlan.Fill(bag, 6, mayCut: true);

        CutWrite write = Assert.Single(PortionCut.Writes(fill, 1, back: 0));

        Assert.Equal(0, write.Lot);
        Assert.Equal(2, write.Packages);
        Assert.Equal(0, write.Stack);
    }

    /// <summary>
    /// Every bag those three sizes make, in up to four slots with two of them the
    /// same size: wherever a fill cuts it meets the order exactly, inside the
    /// position limit, and with every unit accounted for.
    /// </summary>
    [Fact]
    public void Every_cut_of_every_bag_comes_to_the_order_and_loses_nothing()
    {
        var bags = new List<PackageLot[]>();

        for (int a = 0; a <= 3; a++)
        for (int b = 0; b <= 3; b++)
        for (int c = 0; c <= 3; c++)
        for (int d = 0; d <= 2; d++)
        {
            bags.Add(new[]
            {
                new PackageLot(Baggie, a),
                new PackageLot(Jar, b),
                new PackageLot(Jar, c),
                new PackageLot(Brick, d),
            });
        }

        foreach (PackageLot[] bag in bags)
        for (int order = 1; order <= 75; order++)
        {
            PackageFill plain = PackagePlan.Fill(bag, order);
            PackageFill fill = PackagePlan.Fill(bag, order, mayCut: true);

            Assert.Equal(plain.Covers, fill.Covers);

            if (plain.Covers && plain.Units > order)
            {
                // Overshoot left standing is the position limit and nothing else.
                Assert.True(fill.Cuts || plain.Positions == PackagePlan.MostPositions);
            }

            if (!fill.Cuts)
            {
                Assert.Equal(plain.Units, fill.Units);
                continue;
            }

            Assert.Equal(order, fill.Units);
            Assert.True(fill.Positions <= PackagePlan.MostPositions);
            Assert.True(bag[fill.Opened].UnitsPerPackage > fill.Portion);
            Assert.Equal(order, WholeUnits(bag, fill) + fill.Portion);

            PortionSplit split = Split(bag, fill);
            Assert.True(split.Possible);
            Conserved(bag, order, fill, split);
        }
    }
}
