using System.Collections.Generic;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class GradeChoiceTests
{
    private const int Trash = 0;
    private const int Poor = 1;
    private const int Standard = 2;
    private const int Premium = 3;
    private const int Heavenly = 4;

    private static GradePlan Plan(
        int requestedQuality,
        int requestedQuantity,
        float contractPayment,
        int customerStandard,
        params GradeStock[] stock) =>
        GradeChoice.Plan(
            new DeliveryRequest("ogkush", requestedQuality, requestedQuantity, contractPayment, customerStandard),
            stock);

    [Fact]
    public void Takes_the_lowest_grade_that_meets_the_contract()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Standard, 10),
            new GradeStock(Heavenly, 40));

        Assert.True(plan.CanDeliver);
        Assert.Equal(Standard, plan.ChosenQuality);
        Assert.Equal(10, plan.ChosenUnits);
    }

    [Fact]
    public void Steps_up_a_grade_when_the_lower_one_is_short()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Standard, 4),
            new GradeStock(Premium, 10));

        Assert.True(plan.CanDeliver);
        Assert.Equal(Premium, plan.ChosenQuality);
    }

    [Fact]
    public void Never_makes_up_a_delivery_out_of_two_grades_at_once()
    {
        // Four Standard and six Premium would cover ten units, but a mixed
        // delivery changes the quality difference in a way the player did not
        // ask for. One grade, or nothing.
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Standard, 4),
            new GradeStock(Premium, 6));

        Assert.False(plan.CanDeliver);
    }

    [Fact]
    public void Will_not_under_deliver_on_quality()
    {
        GradePlan plan = Plan(
            requestedQuality: Premium,
            requestedQuantity: 5,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Standard, 50));

        Assert.False(plan.CanDeliver);
        Assert.Contains("Premium", plan.Reason);
    }

    [Fact]
    public void Says_so_when_there_is_no_stock_at_all()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 5,
            contractPayment: 400f,
            customerStandard: Poor);

        Assert.False(plan.CanDeliver);
        Assert.Contains("none", plan.Reason);
    }

    [Fact]
    public void The_chosen_grade_earns_no_quality_bonus_when_it_only_meets_the_contract()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Standard, 10));

        GradeOutcome chosen = plan.Outcomes.Single(o => o.Quality == Standard);

        Assert.Equal(0f, chosen.QualityDifference);
        Assert.Equal(0f, chosen.QualityBonus);
    }

    [Fact]
    public void One_tier_above_the_contract_is_worth_fifteen_percent_of_the_payment()
    {
        // Payment * 0.15 * qualityDifference, read off ProcessHandover.
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Premium, 10));

        GradeOutcome premium = plan.Outcomes.Single(o => o.Quality == Premium);

        Assert.Equal(1f, premium.QualityDifference);
        Assert.Equal(60f, premium.QualityBonus, 3);
    }

    [Fact]
    public void Two_tiers_above_pay_twice_as_much()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Heavenly, 10));

        GradeOutcome heavenly = plan.Outcomes.Single(o => o.Quality == Heavenly);

        Assert.Equal(2f, heavenly.QualityDifference);
        Assert.Equal(120f, heavenly.QualityBonus, 3);
    }

    [Fact]
    public void A_quality_difference_below_the_games_threshold_pays_nothing()
    {
        // ProcessHandover only builds the bonus when qualityDifference >= 0.2,
        // and a single-grade delivery only ever lands on whole numbers, so the
        // threshold is stated here rather than assumed away.
        GradeOutcome outcome = GradeChoice.Score(
            quality: Standard,
            requestedQuality: Standard,
            contractPayment: 400f,
            customerStandard: Poor,
            unitsAvailable: 10,
            requestedQuantity: 10);

        Assert.False(outcome.EarnsQualityBonus);
    }

    [Fact]
    public void Reports_every_grade_in_stock_even_the_ones_it_will_not_use()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Poor, 30),
            new GradeStock(Standard, 10),
            new GradeStock(Heavenly, 10));

        Assert.Equal(new[] { Poor, Standard, Heavenly }, plan.Outcomes.Select(o => o.Quality).ToArray());
    }

    [Fact]
    public void A_grade_at_the_customers_standard_clears_it()
    {
        GradeOutcome outcome = GradeChoice.Score(
            quality: Standard,
            requestedQuality: Trash,
            contractPayment: 400f,
            customerStandard: Standard,
            unitsAvailable: 10,
            requestedQuantity: 10);

        Assert.True(outcome.ClearsStandards);
    }

    [Fact]
    public void A_grade_below_the_customers_standard_does_not_clear_it()
    {
        GradeOutcome outcome = GradeChoice.Score(
            quality: Poor,
            requestedQuality: Trash,
            contractPayment: 400f,
            customerStandard: Standard,
            unitsAvailable: 10,
            requestedQuantity: 10);

        Assert.False(outcome.ClearsStandards);
    }

    [Fact]
    public void Meeting_the_contract_matches_the_product_list_in_full()
    {
        GradeOutcome outcome = GradeChoice.Score(
            quality: Standard,
            requestedQuality: Standard,
            contractPayment: 400f,
            customerStandard: Poor,
            unitsAvailable: 10,
            requestedQuantity: 10);

        Assert.Equal(10, outcome.MatchedProductCount);
    }

    [Fact]
    public void A_grade_short_of_the_quantity_matches_only_what_it_covers()
    {
        GradeOutcome outcome = GradeChoice.Score(
            quality: Premium,
            requestedQuality: Standard,
            contractPayment: 400f,
            customerStandard: Poor,
            unitsAvailable: 4,
            requestedQuantity: 10);

        Assert.Equal(4, outcome.MatchedProductCount);
        Assert.False(outcome.CoversTheContract);
    }

    [Fact]
    public void Stock_of_an_unknown_grade_is_ignored_rather_than_guessed_at()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(quality: 9, units: 100));

        Assert.False(plan.CanDeliver);
    }

    [Fact]
    public void A_contract_asking_for_nothing_is_not_delivered()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 0,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Standard, 10));

        Assert.False(plan.CanDeliver);
    }

    [Fact]
    public void The_plan_always_explains_itself()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Poor,
            new GradeStock(Standard, 10));

        Assert.False(string.IsNullOrWhiteSpace(plan.Reason));
    }

    [Fact]
    public void Describes_the_choice_and_what_the_other_grades_would_have_paid()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Premium,
            new GradeStock(Standard, 10),
            new GradeStock(Heavenly, 10));

        IReadOnlyList<string> lines = GradeChoice.Describe(plan, amount => $"${amount:0}");

        Assert.Contains(lines, line => line.Contains("Standard"));
        Assert.Contains(lines, line => line.Contains("Heavenly") && line.Contains("$120"));
        // Standard is below a Premium customer's standards; the report says so
        // rather than leaving the player to work it out.
        Assert.Contains(lines, line => line.Contains("below their standards"));
    }

    [Fact]
    public void Says_plainly_when_nothing_in_reach_meets_the_customers_standards()
    {
        GradePlan plan = Plan(
            requestedQuality: Poor,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Heavenly,
            new GradeStock(Poor, 10));

        IReadOnlyList<string> lines = GradeChoice.Describe(plan, amount => $"${amount:0}");

        Assert.Contains(lines, line => line.Contains("nothing in reach clears them"));
    }

    [Fact]
    public void Says_nothing_about_standards_when_every_grade_clears_them()
    {
        GradePlan plan = Plan(
            requestedQuality: Standard,
            requestedQuantity: 10,
            contractPayment: 400f,
            customerStandard: Trash,
            new GradeStock(Standard, 10));

        IReadOnlyList<string> lines = GradeChoice.Describe(plan, amount => $"${amount:0}");

        Assert.DoesNotContain(lines, line => line.Contains("standards"));
    }
}
