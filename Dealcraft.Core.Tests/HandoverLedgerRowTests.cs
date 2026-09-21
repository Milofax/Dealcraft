using System.Collections.Generic;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class HandoverLedgerRowTests
{
    /// <summary>
    /// The row's most important content: the bonuses as the game named them and
    /// sized them, copied across without a formula in between.
    /// </summary>
    [Fact]
    public void The_shown_bonuses_keep_the_games_own_titles_and_amounts()
    {
        var shown = new ShownPopup(
            BasePayment: 250f,
            Satisfaction: 1f,
            RelationshipDelta: 0.1f,
            Bonuses: new List<ShownBonus>
            {
                new("Curfew Bonus", 50f),
                new("Generosity Bonus", 20f),
            });

        string written = shown.ToJson().ToString();

        Assert.Contains("{\"title\":\"Curfew Bonus\",\"amount\":50}", written);
        Assert.Contains("{\"title\":\"Generosity Bonus\",\"amount\":20}", written);
        Assert.Equal(70f, shown.BonusTotal);
    }

    /// <summary>
    /// The two seams are two readings of one quantity. Where both are present
    /// they should agree, and a row carries both so that they can be compared
    /// rather than trusted.
    /// </summary>
    [Fact]
    public void The_measured_bonus_total_is_what_the_server_moved_less_what_was_promised()
    {
        var row = new HandoverLedgerRow { ContractPayment = 250f, TotalPayment = 320f };

        Assert.Equal(70f, row.MeasuredBonusTotal);
    }

    [Fact]
    public void A_row_with_only_one_side_of_the_subtraction_measures_nothing()
    {
        Assert.Null(new HandoverLedgerRow { TotalPayment = 320f }.MeasuredBonusTotal);
        Assert.Null(new HandoverLedgerRow { ContractPayment = 250f }.MeasuredBonusTotal);
    }

    /// <summary>
    /// Everything the reader needs has to be on the one line, because the file
    /// is read a session later with nothing else to hand.
    /// </summary>
    [Fact]
    public void A_row_is_one_line_of_json_carrying_both_sides_of_the_question()
    {
        var row = new HandoverLedgerRow
        {
            Seam = "server+popup",
            CustomerName = "Jessi Waters",
            HandoverByPlayer = true,
            Outcome = "Finalize",
            ContractPayment = 250f,
            TotalPayment = 320f,
            Satisfaction = 1f,
            RequestedTotalQuantity = 4,
            DeliveredUnits = 5,
            QualityTiers = 0f,
        };

        row.Requested.Add(new RequestedLine("greenCrack", QualityTier.Standard, 4));
        row.Delivered.Add(new DeliveredLine("greenCrack", QualityTier.Standard, 5, 1, 5, "jar"));
        row.Shown = new ShownPopup(250f, 1f, 0.1f, new List<ShownBonus>
        {
            new("Generosity Bonus", 10f),
        });
        row.Prediction = HandoverPrediction.Of(250f, 4, 5, 1f, 0f, false, false, 0f);

        string line = row.ToJson();

        Assert.DoesNotContain("\n", line);
        Assert.Contains("\"customer\":\"Jessi Waters\"", line);
        Assert.Contains("\"handover_by_player\":true", line);
        Assert.Contains("\"measured_bonus_total\":70", line);
        Assert.Contains("\"quality_name\":\"Standard\"", line);
        Assert.Contains("\"packaging\":\"jar\"", line);
        Assert.Contains("\"dealcraft_prediction\":{", line);
        Assert.Contains("\"predicted_generosity\":10", line);
    }

    /// <summary>
    /// A handover another player did by hand produces a row with no popup on
    /// this machine. That row still matters — it is the control group — so it
    /// says why the popup is missing rather than omitting the key.
    /// </summary>
    [Fact]
    public void A_row_without_a_popup_says_so_instead_of_leaving_the_key_out()
    {
        var row = new HandoverLedgerRow { ShownUnavailable = "no popup played here" };

        string line = row.ToJson();

        Assert.Contains("\"shown\":null", line);
        Assert.Contains("\"shown_unavailable\":\"no popup played here\"", line);
    }

    /// <summary>
    /// Four of the five bonuses depend on the world, so a row that could not
    /// read it has to name what it missed — otherwise a disagreement with the
    /// prediction cannot be told from a wrong constant.
    /// </summary>
    [Fact]
    public void A_world_reading_that_failed_names_what_was_missing()
    {
        var row = new HandoverLedgerRow();
        row.World.Unreadable.Add("weather");

        Assert.Contains("\"unreadable\":[\"weather\"]", row.ToJson());
    }

    [Fact]
    public void An_unpackaged_delivery_line_says_so_rather_than_inventing_a_packaging()
    {
        string written = new DeliveredLine("greenCrack", QualityTier.Poor, 1, 3, 3, null)
            .ToJson()
            .ToString();

        Assert.Contains("\"packaging\":null", written);
    }
}
