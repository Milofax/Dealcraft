using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The prediction is the thing the ledger exists to test, so these tests say
/// only one thing: that this type states what the project notes
/// states. They are not evidence that the document is right — nothing in a test
/// project could be. The evidence will be a file full of rows.
/// </summary>
public class HandoverPredictionTests
{
    /// <summary>A handover that earns nothing at all, to build the others from.</summary>
    private static HandoverPrediction Plain(
        float payment = 1000f,
        int requested = 10,
        int delivered = 10,
        float satisfaction = 1f,
        float? tiers = 0f,
        bool? curfew = false,
        bool? quick = false,
        float? rainy = 0f) =>
        HandoverPrediction.Of(payment, requested, delivered, satisfaction, tiers, curfew, quick, rainy);

    [Fact]
    public void A_delivery_that_matches_the_contract_exactly_predicts_nothing()
    {
        HandoverPrediction prediction = Plain();

        Assert.Equal(0f, prediction.Total);
        Assert.True(prediction.IsComplete);
    }

    /// <summary>
    /// The owner's question, and the whole reason for the ledger. Ten dollars an
    /// extra unit, flat — not a share of the payment, and not scaled by
    /// anything.
    /// </summary>
    [Fact]
    public void Generosity_is_ten_dollars_for_every_unit_over_the_order()
    {
        Assert.Equal(40f, Plain(requested: 4, delivered: 8).Generosity);
    }

    /// <summary>The payment does not enter into it, which is the claim under test.</summary>
    [Fact]
    public void Generosity_does_not_move_with_the_payment()
    {
        Assert.Equal(
            Plain(payment: 100f, requested: 4, delivered: 5).Generosity,
            Plain(payment: 9000f, requested: 4, delivered: 5).Generosity);
    }

    [Fact]
    public void Delivering_short_predicts_no_generosity_rather_than_a_negative_one()
    {
        Assert.Equal(0f, Plain(requested: 10, delivered: 4).Generosity);
    }

    /// <summary>
    /// The game builds the bonus only when the delivery scored at least 0.99, so
    /// a row under that gate should not count it — but the figure is still
    /// written down, because a reader has to be able to see what was forgone and
    /// why.
    /// </summary>
    [Fact]
    public void Generosity_is_stated_but_not_counted_when_the_satisfaction_gate_is_missed()
    {
        HandoverPrediction prediction = Plain(requested: 4, delivered: 5, satisfaction: 0.98f);

        Assert.Equal(10f, prediction.Generosity);
        Assert.False(prediction.GenerosityGateMet);
        Assert.Equal(0f, prediction.Total);
    }

    [Fact]
    public void The_gate_is_met_exactly_at_the_games_own_threshold()
    {
        Assert.True(Plain(satisfaction: HandoverPrediction.GenerositySatisfactionGate).GenerosityGateMet);
    }

    [Fact]
    public void Curfew_and_rain_each_pay_a_fifth_of_the_payment()
    {
        Assert.Equal(200f, Plain(curfew: true).Curfew);
        Assert.Equal(200f, Plain(rainy: 0.5f).Rainy);
    }

    /// <summary>
    /// The fifth bonus tests the rain against 0.10, so drizzle at exactly the
    /// threshold pays nothing.
    /// </summary>
    [Fact]
    public void Rain_has_to_exceed_the_threshold_rather_than_reach_it()
    {
        Assert.Equal(0f, Plain(rainy: HandoverPrediction.RainyThreshold).Rainy);
        Assert.Equal(200f, Plain(rainy: 0.11f).Rainy);
    }

    [Fact]
    public void Quick_delivery_pays_a_tenth()
    {
        Assert.Equal(100f, Plain(quick: true).QuickDelivery);
    }

    /// <summary>
    /// Bonus row 3, as measured. Two static readings of the same instructions
    /// disagreed — fifteen percent of the payment a tier against three — and
    /// this test used to assert the three. The ledger settled it on its first
    /// evening: Peter File, three tiers above on a payment of 495.00, was paid
    /// an Exceeded Quality Bonus of 222.75, which is 0.15 x 3 and not
    /// 0.15 x 0.20 x 3. The 0.20 belongs to the gate, not to the money.
    /// </summary>
    [Fact]
    public void One_tier_above_the_contract_predicts_fifteen_percent_of_the_payment()
    {
        Assert.Equal(150f, Plain(tiers: 1f).ExceededQuality);
    }

    [Fact]
    public void Tiers_above_the_first_are_linear()
    {
        Assert.Equal(300f, Plain(tiers: 2f).ExceededQuality);
    }

    /// <summary>
    /// The two handovers that decided it, to the cent, so a future reading of
    /// the reading cannot quietly move the figure back.
    /// </summary>
    [Theory]
    [InlineData(495f, 3f, false, 222.75f)]   // Peter File
    [InlineData(595f, 2f, true, 238f)]       // Louis Fourier, inside the quick window
    public void The_measured_handovers_are_reproduced(
        float payment, float tiers, bool quick, float expected)
    {
        HandoverPrediction prediction = Plain(payment: payment, tiers: tiers, quick: quick);

        Assert.Equal(expected, prediction.ExceededQuality!.Value
            + (prediction.QuickDelivery ?? 0f), precision: 2);
    }

    /// <summary>
    /// The gate is a whole tier, because what is compared against 0.20 is the
    /// difference already scaled by 0.20.
    /// </summary>
    [Fact]
    public void Part_of_a_tier_predicts_no_quality_bonus()
    {
        Assert.Equal(0f, Plain(tiers: 0.5f).ExceededQuality);
    }

    [Fact]
    public void The_total_is_the_five_summed()
    {
        HandoverPrediction prediction = Plain(
            requested: 4, delivered: 6, tiers: 1f, curfew: true, quick: true, rainy: 0.5f);

        // 200 curfew + 20 generosity + 150 quality + 100 quick + 200 rain
        Assert.Equal(670f, prediction.Total);
    }

    /// <summary>
    /// A world reading we could not take must not be silently predicted as zero:
    /// a row that disagrees with the game because it guessed is worse than a row
    /// that says it does not know.
    /// </summary>
    [Fact]
    public void A_bonus_whose_world_reading_is_missing_is_named_rather_than_assumed()
    {
        HandoverPrediction prediction = Plain(curfew: null, rainy: null);

        Assert.Null(prediction.Curfew);
        Assert.Null(prediction.Rainy);
        Assert.False(prediction.IsComplete);
        Assert.Equal(new[] { "curfew", "rainy" }, prediction.Unknown);
    }

    [Fact]
    public void The_prediction_says_in_the_row_that_it_is_not_a_measurement()
    {
        string written = Plain(requested: 4, delivered: 5).ToJson().ToString();

        Assert.Contains("NOT a measurement", written);
        Assert.Contains("\"predicted_generosity\":10", written);
    }
}
