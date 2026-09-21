using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class GenerosityTests
{
    [Fact]
    public void The_bonus_is_ten_dollars_a_unit_flat()
    {
        // docs/handover-truth.md, "The Generosity Bonus, read out in full":
        // (delivered - requested) x 10, in dollars, flat. Not a share of the
        // payment.
        Assert.Equal(10f, Generosity.BonusPerUnit, 4);
    }

    [Fact]
    public void The_bonus_is_capped_by_the_packaging_and_cannot_be_farmed()
    {
        // The game takes ceil(needed / unitsPerPackage) packages and no more,
        // so the overshoot can never exceed one package less one unit.
        Assert.Equal(0f, Generosity.MostBonus(unitsPerPackage: 1), 4);
        Assert.Equal(40f, Generosity.MostBonus(unitsPerPackage: 5), 4);
        Assert.Equal(190f, Generosity.MostBonus(unitsPerPackage: 20), 4);
    }

    [Fact]
    public void A_baggie_can_never_earn_the_bonus_at_all()
    {
        // One unit per package means no overshoot is possible.
        Assert.Equal(0, Generosity.MostExtraUnits(unitsPerPackage: 1));
        Assert.False(Generosity.CanEverEarn(unitsPerPackage: 1));
        Assert.True(Generosity.CanEverEarn(unitsPerPackage: 5));
    }

    [Fact]
    public void Packaging_the_game_gave_no_size_for_earns_nothing_rather_than_throwing()
    {
        Assert.Equal(0f, Generosity.MostBonus(unitsPerPackage: 0), 4);
        Assert.Equal(0f, Generosity.MostBonus(unitsPerPackage: -5), 4);
    }

    [Fact]
    public void The_three_packagings_are_the_ones_the_game_ships()
    {
        // Named and sized in docs/handover-truth.md. The ceiling table on the
        // product panel is these three and nothing else.
        Assert.Equal(
            new[] { "baggie", "jar", "brick" },
            Generosity.Packagings.Select(packaging => packaging.Name));
        Assert.Equal(
            new[] { 1, 5, 20 },
            Generosity.Packagings.Select(packaging => packaging.UnitsPerPackage));
    }

    [Fact]
    public void Each_packaging_carries_its_own_ceiling()
    {
        Assert.Equal(
            new[] { 0f, 40f, 190f },
            Generosity.Packagings.Select(packaging => packaging.MostBonus));
    }

    [Fact]
    public void A_product_dearer_than_the_bonus_loses_the_difference_on_each_extra_unit()
    {
        // Give away a $35 unit, earn $10.
        Assert.Equal(-25f, Generosity.NetPerUnit(35f), 4);
    }

    [Fact]
    public void A_product_cheaper_than_the_bonus_is_the_only_case_that_wins()
    {
        Assert.Equal(2f, Generosity.NetPerUnit(8f), 4);
    }

    [Fact]
    public void A_product_listed_at_exactly_the_bonus_is_a_wash()
    {
        Assert.Equal(0f, Generosity.NetPerUnit(10f), 4);
    }
}
