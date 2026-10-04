using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The arithmetic of opening one package: which one, how much crosses, how much
/// comes back, and whether the remainder needs a slot of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>These exist because the first version had none of them.</b> The same sums
/// were worked out inside the code that moves the goods, where the only way to
/// check them was to play the game and count what was left, and what it got
/// wrong was a slot holding more than one package — which is the case nobody
/// thinks to try by hand. A review read it and named the number: a slot of
/// twenty jars against an order of three left one jar and destroyed nineteen,
/// ninety-five units, and the code that did it was the rollback written to make
/// a failed cut safe.
/// </para>
/// <para>
/// <see cref="CuttingAPortionTests"/> holds the decision — whether the cut is
/// worth making. These hold what it comes to.
/// </para>
/// </remarks>
public class OpeningOnePackageTests
{
    private const int Baggie = 1;
    private const int Jar = 5;
    private const int Brick = 20;

    /// <summary>The three packagings the game gives a product, smallest first.</summary>
    private static readonly int[] Packagings = { Baggie, Jar, Brick };

    /// <summary>
    /// Packages one inventory slot holds.
    /// </summary>
    /// <remarks>
    /// <b>Twenty, and not by assumption.</b> The owner's save carries fifteen
    /// baggies of Remington Steele in one slot, so the limit is at least fifteen,
    /// and he reads twenty off the game. The code never assumes it — it asks
    /// <c>ItemDefinition.StackLimit</c> and <c>ItemSlot.GetCapacityForItem</c> — and
    /// the log now prints the figure with every cut, so the next session settles it
    /// for good. These tests model twenty because that is what the evidence says,
    /// and <see cref="A_smaller_slot_changes_the_packaging"/> holds the rule rather
    /// than the number.
    /// </remarks>
    private const int Slot = 20;

    private static PortionSplit Split(int packagesInSlot, int unitsPerPackage, int order) =>
        PortionCut.Split(packagesInSlot, unitsPerPackage, order, Packagings, Slot);

    /// <summary>What the remainder comes to.</summary>
    private static int Units(System.Collections.Generic.IReadOnlyList<PortionStack> stacks)
    {
        int units = 0;

        foreach (PortionStack stack in stacks)
        {
            units += stack.Units;
        }

        return units;
    }

    /// <summary>The remainder as (units per package, packages) pairs, in order.</summary>
    private static (int, int)[] Shape(
        System.Collections.Generic.IReadOnlyList<PortionStack> stacks)
    {
        var shape = new (int, int)[stacks.Count];

        for (int i = 0; i < stacks.Count; i++)
        {
            shape[i] = (stacks[i].UnitsPerPackage, stacks[i].Packages);
        }

        return shape;
    }

    /// <summary>
    /// The case the review found, as its own test. Twenty jars is one slot and
    /// one lot, and opening one of them must leave nineteen.
    /// </summary>
    [Fact]
    public void Twenty_jars_against_an_order_of_three_needs_a_slot_of_its_own()
    {
        PortionSplit split = Split(packagesInSlot: 20, unitsPerPackage: Jar, order: 3);

        Assert.True(split.Possible);
        Assert.Equal(3, split.Portions);
        Assert.Equal(Baggie, split.PortionUnits);
        Assert.Equal(2, Units(split.Leftover));

        // The whole of the defect in one assertion: the other nineteen jars are
        // still in that slot, so the two baggies cannot go back into it.
        Assert.Equal(1, split.SlotsNeeded);
    }

    /// <summary>
    /// One package in the slot, and the slot empties itself as it leaves — so
    /// the remainder goes straight back where it came from and the bag needs no
    /// room at all. The ordinary case: nobody carries twenty bricks.
    /// </summary>
    [Fact]
    public void One_brick_alone_in_its_slot_needs_no_second_slot()
    {
        PortionSplit split = Split(packagesInSlot: 1, unitsPerPackage: Brick, order: 5);

        Assert.True(split.Possible);

        // One jar out: the largest packaging that divides five, so one position on
        // the handover screen rather than five.
        Assert.Equal(Jar, split.PortionUnits);
        Assert.Equal(1, split.Portions);

        // Fifteen baggies back, in one stack. Three jars would be the same fifteen
        // units in the same one place, and baggies are still better: the next order
        // of any size is served straight out of them.
        Assert.Equal(new[] { (Baggie, 15) }, Shape(split.Leftover));

        // The brick's slot empties itself as the brick leaves, so that stack goes
        // straight back into it and the bag needs no room at all.
        Assert.Equal(0, split.SlotsNeeded);
    }

    /// <summary>
    /// Two bricks is a different answer from one, and the only thing that
    /// changed is the count in the slot.
    /// </summary>
    [Fact]
    public void Two_bricks_in_one_slot_need_a_second_slot()
    {
        PortionSplit one = Split(1, Jar, 3);
        PortionSplit two = Split(2, Jar, 3);

        Assert.Equal(one.Portions, two.Portions);
        Assert.Equal(Shape(one.Leftover), Shape(two.Leftover));
        Assert.Equal(0, one.SlotsNeeded);
        Assert.Equal(1, two.SlotsNeeded);
    }

    /// <summary>
    /// Nothing is created and nothing is lost: what crosses plus what comes back
    /// is the package that was opened, at every order a jar can be cut to.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void What_crosses_plus_what_comes_back_is_the_package(int order)
    {
        PortionSplit split = Split(4, Jar, order);

        Assert.True(split.Possible);
        Assert.Equal(order, split.Portions * split.PortionUnits);
        Assert.Equal(Jar, (split.Portions * split.PortionUnits) + Units(split.Leftover));
    }

    /// <summary>
    /// An order the package holds exactly is not a cut. There is nothing to cut
    /// off it, and the ordinary fill already hands it over whole.
    /// </summary>
    [Fact]
    public void A_package_that_holds_exactly_the_order_is_not_opened()
    {
        Assert.False(Split(1, Jar, 5).Possible);
        Assert.False(Split(1, Jar, 6).Possible);
    }

    /// <summary>
    /// An empty slot has nothing to open, and an order of nothing asks for no
    /// cut. Neither is a crash and neither is a cut.
    /// </summary>
    [Fact]
    public void Nothing_in_the_slot_and_nothing_ordered_are_both_no_cut()
    {
        Assert.False(Split(0, Brick, 6).Possible);
        Assert.False(Split(1, Brick, 0).Possible);
        Assert.False(Split(1, Brick, -3).Possible);
    }

    /// <summary>
    /// Whole packages on both sides or no cut at all. A part-filled package would
    /// either short-change the customer or invent product.
    /// </summary>
    [Fact]
    public void A_portion_that_does_not_divide_is_refused()
    {
        // Packagings of three and seven: a brick of twenty cut at five leaves
        // fifteen, which is five threes — but five is neither a three nor a
        // seven, so there is no cut.
        Assert.False(PortionCut.Split(1, Brick, 5, new[] { 3, 7 }, Slot).Possible);

        // And the other way round: nine out is three threes, and the eleven left
        // is neither. Both sides have to divide.
        Assert.False(PortionCut.Split(1, Brick, 9, new[] { 3, 7 }, Slot).Possible);

        // And both sides dividing stands: six out as three twos, fourteen back as
        // seven twos - the smallest that makes one stack, for what comes back.
        PortionSplit split = PortionCut.Split(1, Brick, 6, new[] { 2, 7 }, Slot);
        Assert.True(split.Possible);
        Assert.Equal(2, split.PortionUnits);
        Assert.Equal(3, split.Portions);
        Assert.Equal(new[] { (2, 7) }, Shape(split.Leftover));
    }

    /// <summary>
    /// <b>The largest packaging that divides it, because that is the fewest
    /// packages.</b> Fifteen units back is three jars and not fifteen baggies —
    /// which is both what the owner asked for, <i>"ich komme gar nicht mehr mit
    /// den Tüten hinterher"</i>, and what fits a slot at all.
    /// </summary>
    [Fact]
    public void The_portion_takes_the_largest_packaging_that_divides_it()
    {
        PortionSplit ten = Split(1, Brick, 10);

        Assert.True(ten.Possible);

        // Out as two jars: one position on the handover screen and the fewest
        // packages for it. The customer has no use for ten baggies.
        Assert.Equal(Jar, ten.PortionUnits);
        Assert.Equal(2, ten.Portions);

        // Back as ten baggies, for the opposite reason: what comes back is what the
        // next contracts are served from.
        Assert.Equal(new[] { (Baggie, 10) }, Shape(ten.Leftover));
    }

    /// <summary>
    /// <b>The case that used to lose product, and then used to be refused.</b>
    /// Three units out of a brick leaves seventeen; no packaging divides
    /// seventeen but the baggie, and seventeen baggies is more than a slot holds.
    /// Writing them into the slot destroyed what was there. Refusing the cut
    /// instead meant bricks were never cut at all, which the owner called what it
    /// was.
    /// </summary>
    /// <remarks>
    /// So the remainder is mixed, which is what the game itself hands back at a
    /// packaging station: three jars and two baggies, two stacks, two places in
    /// the bag.
    /// </remarks>
    [Fact]
    public void Seventeen_left_over_comes_back_as_one_stack_of_baggies()
    {
        PortionSplit split = Split(1, Brick, 3);

        Assert.True(split.Possible);
        Assert.Equal(3, split.Portions * split.PortionUnits);

        // Seventeen baggies, one place in the bag. Three jars and two baggies is
        // the same seventeen units in TWO places, which is what this used to do and
        // what the owner counted: "Ich brauche pro Produkt immer drei Plätze."
        Assert.Equal(new[] { (Baggie, 17) }, Shape(split.Leftover));
        Assert.Equal(17, Units(split.Leftover));

        // And the brick's own slot takes it as the brick leaves, so it costs
        // nothing at all.
        Assert.Equal(0, split.SlotsNeeded);
    }

    /// <summary>
    /// <b>A stack that fits beside its own kind costs no place in the bag.</b> The
    /// owner carries bricks and baggies of the same product, and a cut's remainder
    /// belongs on the baggies he already has rather than in a slot of its own.
    /// </summary>
    [Fact]
    public void A_remainder_that_fits_beside_its_own_kind_is_packaged_to_do_so()
    {
        // Fourteen back. It divides into jars as well - but he has room for
        // fourteen baggies beside his own, and none for jars.
        PortionSplit beside = PortionCut.Split(
            packagesInSlot: 5,
            unitsPerPackage: Brick,
            order: 6,
            packagings: Packagings,
            stackLimit: Slot,
            roomBeside: new[] { 14, 0, 0 });

        Assert.True(beside.Possible);
        Assert.Equal(new[] { (Baggie, 14) }, Shape(beside.Leftover));

        // Room for the jars instead, and it takes the jars.
        PortionSplit jars = PortionCut.Split(
            packagesInSlot: 5,
            unitsPerPackage: Brick,
            order: 5,
            packagings: Packagings,
            stackLimit: Slot,
            roomBeside: new[] { 0, 3, 0 });

        Assert.True(jars.Possible);
        Assert.Equal(new[] { (Jar, 3) }, Shape(jars.Leftover));
    }

    /// <summary>
    /// With nothing to merge into, the largest packaging that still makes one stack
    /// wins — fewest packages for the same one place.
    /// </summary>
    [Fact]
    public void With_nothing_to_merge_into_the_smallest_single_stack_wins()
    {
        PortionSplit split = Split(5, Brick, 5);

        Assert.True(split.Possible);

        // Fifteen baggies, not three jars. One place in the bag either way, and
        // baggies answer the next contract of any size without another cut.
        Assert.Equal(new[] { (Baggie, 15) }, Shape(split.Leftover));
    }

    /// <summary>
    /// The rule and not the number: a slot that holds fewer packages forces the
    /// remainder into more than one, and the arithmetic says so rather than
    /// refusing.
    /// </summary>
    [Fact]
    public void A_smaller_slot_changes_the_packaging()
    {
        // Seventeen back, ten to a slot: no single packaging manages it, so it
        // falls back to biggest-first and comes to two stacks.
        PortionSplit tight = PortionCut.Split(1, Brick, 3, Packagings, stackLimit: 10);

        Assert.True(tight.Possible);
        Assert.Equal(new[] { (Jar, 3), (Baggie, 2) }, Shape(tight.Leftover));

        // The same cut in a slot of twenty is one stack.
        Assert.Single(Split(1, Brick, 3).Leftover);
    }

    /// <summary>
    /// The same brick out of a slot that holds two of them: both stacks need a
    /// place, because the slot keeps the other brick.
    /// </summary>
    [Fact]
    public void Two_bricks_in_a_slot_need_one_free_slot_for_the_remainder()
    {
        PortionSplit split = Split(2, Brick, 3);

        Assert.True(split.Possible);

        // One stack, so one place - and the slot keeps the other brick, so that
        // place has to be found somewhere else.
        Assert.Single(split.Leftover);
        Assert.Equal(1, split.SlotsNeeded);
    }

    /// <summary>
    /// A remainder needing more places than the handover screen has is not a cut
    /// worth making. Four is the bound, and it is the game's own.
    /// </summary>
    [Fact]
    public void A_remainder_needing_more_than_four_stacks_is_no_cut()
    {
        // Nine baggies to a slot and a package of a hundred: cutting one unit off
        // leaves ninety-nine, which is eleven stacks of nine.
        Assert.False(PortionCut.Split(1, 100, 1, new[] { Baggie }, stackLimit: 9).Possible);

        // The same hundred with jars and bricks available comes back in four.
        PortionSplit split = PortionCut.Split(1, 100, 5, Packagings, stackLimit: 9);
        Assert.True(split.Possible);
        Assert.Equal(95, Units(split.Leftover));
        Assert.True(split.Leftover.Count <= PortionCut.MostStacks);
    }

    /// <summary>
    /// A slot that holds nothing is not a bound to divide by, and neither is a
    /// missing list of packagings.
    /// </summary>
    [Fact]
    public void No_stack_limit_and_no_packagings_are_both_no_cut()
    {
        Assert.False(PortionCut.Split(1, Brick, 6, Packagings, stackLimit: 0).Possible);
        Assert.False(PortionCut.Split(1, Brick, 6, packagings: null!, stackLimit: Slot).Possible);
        Assert.False(PortionCut.Split(1, Brick, 6, new int[0], Slot).Possible);
    }

    /// <summary>
    /// A packaging of no units is a division by nothing. It cannot happen and it
    /// must not throw — it is skipped, and the ones beside it still answer.
    /// </summary>
    [Fact]
    public void A_packaging_of_no_units_is_stepped_over_rather_than_divided_by()
    {
        Assert.False(PortionCut.Split(1, Brick, 6, new[] { 0 }, Slot).Possible);

        PortionSplit split = PortionCut.Split(1, Brick, 10, new[] { 0, Baggie, Jar }, Slot);
        Assert.True(split.Possible);
        Assert.Equal(10, split.Portions * split.PortionUnits);
        Assert.Equal(10, Units(split.Leftover));
    }

    /// <summary>
    /// The smallest package that holds more than the order is the one opened, so
    /// a brick is left alone while a jar will do.
    /// </summary>
    [Fact]
    public void The_smallest_package_worth_opening_is_the_one_chosen()
    {
        var bag = new[]
        {
            new PackageLot(Brick, 2),
            new PackageLot(Jar, 3),
            new PackageLot(Baggie, 1),
        };

        Assert.Equal(1, PortionCut.Opened(bag, 3));
    }

    /// <summary>
    /// A lot with no packages in it is not a candidate however well its size
    /// would fit.
    /// </summary>
    [Fact]
    public void An_empty_lot_is_not_opened()
    {
        var bag = new[] { new PackageLot(Jar, 0), new PackageLot(Brick, 1) };

        Assert.Equal(1, PortionCut.Opened(bag, 3));
    }

    /// <summary>
    /// Two lots of the same size: the first is taken, and which one hardly
    /// matters — what matters is that the answer is the same every time, because
    /// the plan and the taking both ask and must agree.
    /// </summary>
    [Fact]
    public void Two_lots_of_the_same_size_answer_the_same_way_every_time()
    {
        var bag = new[] { new PackageLot(Jar, 1), new PackageLot(Jar, 4) };

        Assert.Equal(0, PortionCut.Opened(bag, 3));
        Assert.Equal(0, PortionCut.Opened(bag, 3));
    }

    /// <summary>
    /// Nothing bigger than the order means nothing to open, and an order of
    /// nothing asks for nothing.
    /// </summary>
    [Fact]
    public void Nothing_bigger_than_the_order_is_nothing_to_open()
    {
        var bag = new[] { new PackageLot(Baggie, 30) };

        Assert.Equal(-1, PortionCut.Opened(bag, 6));
        Assert.Equal(-1, PortionCut.Opened(new[] { new PackageLot(Brick, 1) }, 0));
        Assert.Equal(-1, PortionCut.Opened(lots: null!, order: 6));
    }

    /// <summary>
    /// <b>The plan and the taking ask the same question of the same bag.</b>
    /// Wherever the plan says it cuts, the package it names is the one
    /// <see cref="PortionCut.Opened"/> chooses — they are the same call now, and
    /// this holds them to it.
    /// </summary>
    [Fact]
    public void Wherever_the_plan_cuts_it_cuts_the_package_that_would_be_opened()
    {
        var sizes = new[] { Baggie, Jar, Brick };

        for (int a = 0; a <= 2; a++)
        for (int b = 0; b <= 2; b++)
        for (int c = 0; c <= 2; c++)
        for (int order = 1; order <= 12; order++)
        {
            var bag = new[]
            {
                new PackageLot(sizes[0], a),
                new PackageLot(sizes[1], b),
                new PackageLot(sizes[2], c),
            };

            PackageFill fill = PackagePlan.Fill(bag, order, mayCut: true);

            if (!fill.Cuts)
            {
                continue;
            }

            int opened = PortionCut.Opened(bag, order);

            Assert.True(opened >= 0, $"the plan cut a bag with nothing to open, order {order}");
            Assert.Equal(1, fill.From(opened));
            Assert.Equal(order, fill.Units);
            Assert.Equal(1, fill.Positions);
        }
    }

    /// <summary>
    /// <b>Cutting never rescues a bag that could not cover the order.</b> That
    /// was written as though it might — the branch tested shortage as well as
    /// overshoot — and it was unreachable, because a package worth opening covers
    /// the order on its own. The claim is now the condition, and this is the
    /// proof of it across every bag those three sizes can make.
    /// </summary>
    [Fact]
    public void A_bag_that_cannot_cover_the_order_is_not_rescued_by_cutting()
    {
        var sizes = new[] { Baggie, Jar, Brick };

        for (int a = 0; a <= 2; a++)
        for (int b = 0; b <= 2; b++)
        for (int c = 0; c <= 2; c++)
        for (int order = 1; order <= 30; order++)
        {
            var bag = new[]
            {
                new PackageLot(sizes[0], a),
                new PackageLot(sizes[1], b),
                new PackageLot(sizes[2], c),
            };

            PackageFill plain = PackagePlan.Fill(bag, order);
            PackageFill cutting = PackagePlan.Fill(bag, order, mayCut: true);

            Assert.Equal(plain.Covers, cutting.Covers);

            if (cutting.Cuts)
            {
                // And it only ever cuts where the ordinary fill overshot.
                Assert.True(plain.Units > order);
            }
        }
    }

    /// <summary>
    /// A fill that takes whole packages does not claim to cut, so the caller can
    /// tell a handover that came to the order exactly from one that overshot.
    /// </summary>
    [Fact]
    public void A_fill_of_whole_packages_does_not_claim_to_cut()
    {
        Assert.False(PackagePlan.Fill(new[] { new PackageLot(Baggie, 20) }, 6, mayCut: true).Cuts);
        Assert.False(PackagePlan.Fill(new[] { new PackageLot(Brick, 1) }, 6).Cuts);
        Assert.True(PackagePlan.Fill(new[] { new PackageLot(Brick, 1) }, 6, mayCut: true).Cuts);
    }
}
