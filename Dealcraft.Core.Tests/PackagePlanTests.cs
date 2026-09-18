using System;
using System.Collections.Generic;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The bag the owner's scenarios were played through with, and the table the
/// ticket made out of it. Two baggies, one jar, one brick: 27 units.
/// </summary>
public class PackagePlanTests
{
    private const int Baggie = 1;
    private const int Jar = 5;
    private const int Brick = 20;

    private static IReadOnlyList<PackageLot> Bag() => new[]
    {
        new PackageLot(Baggie, 2),
        new PackageLot(Jar, 1),
        new PackageLot(Brick, 1),
    };

    /// <summary>
    /// The whole of ticket 14, as a table. Every order from 1 to 25 against that
    /// bag, and the least total that covers it. Worked by hand from the packages
    /// available: nothing between 7 (jar and both baggies) and 20 (the brick)
    /// exists, which is what makes order 8 the case that mattered.
    /// </summary>
    public static TheoryData<int, int> EveryOrder()
    {
        var table = new TheoryData<int, int>();
        int[] least = { 1, 2, 5, 5, 5, 6, 7, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 21, 22, 25, 25, 25 };

        for (int order = 1; order <= least.Length; order++)
        {
            table.Add(order, least[order - 1]);
        }

        return table;
    }

    [Theory]
    [MemberData(nameof(EveryOrder))]
    public void Hands_over_the_least_that_covers_the_order(int order, int least)
    {
        PackageFill fill = PackagePlan.Fill(Bag(), order);

        Assert.True(fill.Covers);
        Assert.Equal(least, fill.Units);
        Assert.True(fill.Units >= order, "a delivery is never short");
    }

    /// <summary>
    /// The case that made the ticket. Smallest-package-first put both baggies
    /// and the jar in, still needed the brick, and emptied a 27-unit bag into a
    /// contract for 8 — about $1,000 of product at a listed $52, for $190 of
    /// bonus. The brick alone is the answer.
    /// </summary>
    [Fact]
    public void An_order_of_eight_hands_over_the_brick_alone()
    {
        PackageFill fill = PackagePlan.Fill(Bag(), 8);

        Assert.Equal(20, fill.Units);
        Assert.Equal(1, fill.Packages);
        Assert.Equal(0, fill.From(0));
        Assert.Equal(0, fill.From(1));
        Assert.Equal(1, fill.From(2));
    }

    /// <summary>
    /// Where two combinations come to the same units, the one using fewer
    /// packages wins — the mod does not fill four handover positions where one
    /// would do. Five baggies and one jar both come to five.
    /// </summary>
    [Fact]
    public void A_tie_on_units_goes_to_the_fewer_packages()
    {
        var bag = new[] { new PackageLot(Baggie, 5), new PackageLot(Jar, 1) };

        PackageFill fill = PackagePlan.Fill(bag, 5);

        Assert.Equal(5, fill.Units);
        Assert.Equal(1, fill.Packages);
        Assert.Equal(1, fill.From(1));
    }

    /// <summary>
    /// The owner's example, and the reason a limit must never block: an order of
    /// six against two jars of five. One jar is short, two are four over, and
    /// nothing between them exists. Ten goes over, and the row says why.
    /// </summary>
    [Fact]
    public void Where_only_one_combination_covers_the_order_it_is_taken_however_far_over()
    {
        PackageFill fill = PackagePlan.Fill(new[] { new PackageLot(Jar, 2) }, 6);

        Assert.True(fill.Covers);
        Assert.Equal(10, fill.Units);
        Assert.Equal(2, fill.Packages);
    }

    /// <summary>
    /// The other owner case: <i>"der will 20, ich habe aber nur 18"</i>. Nothing
    /// is handed over, and it is the shortfall that is reported rather than a
    /// packaging problem.
    /// </summary>
    [Fact]
    public void A_bag_that_cannot_cover_the_order_hands_over_nothing_and_says_what_is_carried()
    {
        PackageFill fill = PackagePlan.Fill(new[] { new PackageLot(Jar, 3), new PackageLot(Baggie, 3) }, 20);

        Assert.False(fill.Covers);
        Assert.Equal(PackageFillOutcome.NotEnoughCarried, fill.Outcome);
        Assert.Equal(0, fill.Units);
        Assert.Equal(18, fill.Carrying);
    }

    /// <summary>
    /// Enough in the bag, but spread over more slots than the handover screen
    /// has positions — and four is what it holds. A player could not do it by
    /// hand either, so neither does the mod, and the row does not blame the
    /// stock.
    /// </summary>
    /// <remarks>
    /// This used to put twenty-five baggies in one lot and expect a refusal,
    /// because the bound was on packages. A lot is one inventory slot and a
    /// position takes the whole stack, so twenty-five baggies in one slot are
    /// one position and go across fine. Five slots holding one baggie each are
    /// what five positions look like.
    /// </remarks>
    [Fact]
    public void Enough_product_in_too_many_slots_is_a_different_answer_from_too_little()
    {
        PackageFill fill = PackagePlan.Fill(
            new[]
            {
                new PackageLot(Baggie, 1),
                new PackageLot(Baggie, 1),
                new PackageLot(Baggie, 1),
                new PackageLot(Baggie, 1),
                new PackageLot(Baggie, 1),
            },
            5);

        Assert.False(fill.Covers);
        Assert.Equal(PackageFillOutcome.NeedsMorePackages, fill.Outcome);
        Assert.Equal(5, fill.Carrying);
    }

    /// <summary>
    /// And the case the owner actually met: five units asked for, thirteen
    /// baggies in one slot. One position, and it goes.
    /// </summary>
    [Fact]
    public void A_whole_stack_out_of_one_slot_is_one_position()
    {
        PackageFill fill = PackagePlan.Fill(new[] { new PackageLot(Baggie, 13) }, 5);

        Assert.True(fill.Covers);
        Assert.Equal(5, fill.Units);
        Assert.Equal(1, fill.Positions);
    }

    [Fact]
    public void Never_more_than_the_four_positions_the_handover_screen_holds()
    {
        var bag = new[] { new PackageLot(Baggie, 9) };

        for (int order = 1; order <= 4; order++)
        {
            Assert.True(PackagePlan.Fill(bag, order).Positions <= PackagePlan.MostPositions);
        }
    }

    /// <summary>
    /// How the slots are spelled must not change the answer: two baggies in one
    /// stack and two baggies in two slots are two baggies.
    /// </summary>
    [Fact]
    public void One_stack_of_two_and_two_stacks_of_one_are_the_same_bag()
    {
        PackageFill stacked = PackagePlan.Fill(
            new[] { new PackageLot(Baggie, 2), new PackageLot(Jar, 1) }, 6);
        PackageFill spread = PackagePlan.Fill(
            new[] { new PackageLot(Baggie, 1), new PackageLot(Baggie, 1), new PackageLot(Jar, 1) }, 6);

        Assert.Equal(stacked.Units, spread.Units);
        Assert.Equal(stacked.Packages, spread.Packages);
    }

    [Fact]
    public void An_empty_bag_covers_nothing()
    {
        PackageFill fill = PackagePlan.Fill(Array.Empty<PackageLot>(), 5);

        Assert.False(fill.Covers);
        Assert.Equal(0, fill.Carrying);
    }

    /// <summary>
    /// Whatever the bag and whatever the order, the answer beats the old rule or
    /// matches it. Smallest-package-first is walked here as it was written, and
    /// its total is never the smaller of the two.
    /// </summary>
    /// <remarks>
    /// Compared only where the old rule stayed inside four packages too. It did
    /// not always: three baggies, a jar and two bricks against an order of 28
    /// makes 28 out of five packages, where four packages cannot do better than
    /// 40. That is the four-package cap costing units, not this rule — see the
    /// note on <see cref="PackagePlan.MostPositions"/>.
    /// </remarks>
    [Fact]
    public void It_is_never_worse_than_the_smallest_package_first_rule_it_replaced()
    {
        var sizes = new[] { 1, 5, 20 };
        int worseBefore = 0;

        foreach (int baggies in new[] { 0, 1, 2, 3 })
        foreach (int jars in new[] { 0, 1, 2 })
        foreach (int bricks in new[] { 0, 1, 2 })
        {
            var bag = new[]
            {
                new PackageLot(sizes[0], baggies),
                new PackageLot(sizes[1], jars),
                new PackageLot(sizes[2], bricks),
            };

            for (int order = 1; order <= 30; order++)
            {
                PackageFill fill = PackagePlan.Fill(bag, order);
                int before = SmallestFirst(bag, order, out int beforePackages);

                if (!fill.Covers || before < order || beforePackages > PackagePlan.MostPositions)
                {
                    continue;
                }

                Assert.True(
                    fill.Units <= before,
                    $"order {order} from {baggies}/{jars}/{bricks}: {fill.Units} against {before}");

                if (fill.Units < before)
                {
                    worseBefore++;
                }
            }
        }

        Assert.True(worseBefore > 0, "the old rule was supposed to be worse somewhere");
    }

    /// <summary>The rule this replaced, walked exactly as <c>Take</c> walked it.</summary>
    private static int SmallestFirst(IReadOnlyList<PackageLot> lots, int order, out int used)
    {
        int taken = 0;
        used = 0;

        foreach (PackageLot lot in lots.OrderBy(lot => lot.UnitsPerPackage))
        {
            if (taken >= order)
            {
                break;
            }

            int wanted = (order - taken + lot.UnitsPerPackage - 1) / lot.UnitsPerPackage;
            int packages = Math.Min(wanted, lot.Packages);
            taken += packages * lot.UnitsPerPackage;
            used += packages;
        }

        return taken;
    }
}
