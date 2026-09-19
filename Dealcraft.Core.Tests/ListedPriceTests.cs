using System.Collections.Generic;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The one switch that changes the game's own state for everybody, and the
/// question it is drawn as.
/// </summary>
public class ListedPriceTests
{
    /// <summary>
    /// Two customers: one clears $40 a unit for ten, one clears $60 for two.
    /// $40 takes $480 a week across both; $60 takes $120 from one. So the
    /// recommendation is $40, and that is what gets written.
    /// </summary>
    private static IReadOnlyList<ProductCandidate> Buyers() => new[]
    {
        new ProductCandidate("Beth Penn", 10, 400f, 0.8f),
        new ProductCandidate("Chloe Bowers", 2, 120f, 0.7f),
    };

    [Fact]
    public void Off_writes_nothing_for_any_product()
    {
        ListedPriceDecision decision = ListedPriceDecision.For(
            maintaining: false, "Green Crack", 25f, PricingRecommendation.Best(Buyers()));

        Assert.Equal(ListedPriceOutcome.LeaveAlone, decision.Outcome);
        Assert.Equal(0f, decision.Price);
    }

    [Fact]
    public void On_writes_the_price_the_customers_still_clear()
    {
        ListedPriceDecision decision = ListedPriceDecision.For(
            maintaining: true, "Green Crack", 25f, PricingRecommendation.Best(Buyers()));

        Assert.Equal(ListedPriceOutcome.Write, decision.Outcome);
        Assert.Equal(40f, decision.Price);
    }

    /// <summary>
    /// A price write changes every future offer from every customer, including
    /// the ones a dealer handles. It says what changed from what to what, and it
    /// says how many customers are still in reach at the new figure.
    /// </summary>
    [Fact]
    public void A_write_says_what_it_moved_and_who_still_clears_it()
    {
        ListedPriceDecision decision = ListedPriceDecision.For(
            maintaining: true, "Green Crack", 25f, PricingRecommendation.Best(Buyers()));

        Assert.Contains("Green Crack", decision.Reason);
        Assert.Contains("$25", decision.Reason);
        Assert.Contains("$40", decision.Reason);
        Assert.Contains("2 of your customers", decision.Reason);
    }

    [Fact]
    public void A_price_already_where_it_belongs_is_not_written_again()
    {
        ListedPriceDecision decision = ListedPriceDecision.For(
            maintaining: true, "Green Crack", 40f, PricingRecommendation.Best(Buyers()));

        Assert.Equal(ListedPriceOutcome.AlreadyThere, decision.Outcome);
    }

    /// <summary>
    /// The game rounds a written price to a whole dollar before it stores it, so
    /// a listed price that differs from the computed one by less than that is
    /// the same price — otherwise every pass would rewrite it for ever.
    /// </summary>
    [Fact]
    public void A_difference_the_game_would_round_away_is_not_a_difference()
    {
        var oddMoney = new[] { new ProductCandidate("Beth Penn", 3, 121f, 0.8f) };

        ListedPriceDecision decision = ListedPriceDecision.For(
            maintaining: true, "Green Crack", 40f, PricingRecommendation.Best(oddMoney));

        Assert.Equal(ListedPriceOutcome.AlreadyThere, decision.Outcome);
    }

    /// <summary>
    /// A product nobody the player knows could order has no computable price.
    /// It is named rather than skipped in silence — the block counts it, and the
    /// log says which one and what it was left at.
    /// </summary>
    [Fact]
    public void A_product_with_no_buyer_is_left_alone_and_said_so()
    {
        ListedPriceDecision decision = ListedPriceDecision.For(
            maintaining: true,
            "Green Crack",
            25f,
            PricingRecommendation.Best(System.Array.Empty<ProductCandidate>()));

        Assert.Equal(ListedPriceOutcome.CannotCompute, decision.Outcome);
        Assert.Contains("Green Crack", decision.Reason);
        Assert.Contains("$25", decision.Reason);
    }

    // --- the question, as the tab draws it -----------------------------------

    /// <summary>
    /// One question with two answers, and exactly one of them true. Not a tick
    /// box: the game's own behaviour is a policy somebody chose, not the absence
    /// of one.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_question_has_exactly_one_answer_ticked(bool maintaining)
    {
        IReadOnlyList<FormRow> rows = PriceMaintenanceCatalog.Rows(
            PriceMaintenanceCatalog.Describe(maintaining).Single());

        Assert.Equal(
            new[] { PriceMaintenanceCatalog.Manual, PriceMaintenanceCatalog.Automated },
            rows.Select(row => row.Id));
        Assert.All(rows, row => Assert.Equal(FormRowKind.Choice, row.Kind));
        Assert.Single(rows, row => row.Chosen);
        Assert.Equal(maintaining, rows[1].Chosen);
    }

    /// <summary>
    /// Either answer writes the whole question, including the one already
    /// ticked. A radio whose selected option does nothing when pressed is a
    /// radio that can be left showing something the file does not say.
    /// </summary>
    [Fact]
    public void Either_answer_writes_the_entry_and_they_write_opposite_values()
    {
        IReadOnlyList<FormRow> rows = PriceMaintenanceCatalog.Rows(
            PriceMaintenanceCatalog.Describe(maintaining: false).Single());

        SettingChange mine = Assert.Single(rows[0].Press);
        SettingChange maintained = Assert.Single(rows[1].Press);

        Assert.Equal(PriceMaintenanceCatalog.Key, mine.Key);
        Assert.Equal(PriceMaintenanceCatalog.Key, maintained.Key);
        Assert.Equal("Off", mine.Value);
        Assert.Equal("On", maintained.Value);
    }

    [Fact]
    public void It_is_off_on_a_fresh_install()
    {
        Assert.False(PriceMaintenanceCatalog.Describe(maintaining: false).Single().SwitchedOn);
    }

    /// <summary>
    /// The block carries no line under it, and the drawing is why: the two
    /// "Automated" answers name what each of the two price blocks automates, so
    /// neither needs a sentence beside it.
    /// </summary>
    /// <remarks>
    /// What the line said is still true and is now unsaid on screen: turning
    /// this off leaves prices where the last pass put them, because restoring is
    /// a different behaviour and nobody asked for it. It goes to the debug file
    /// with every other decision.
    /// </remarks>
    [Fact]
    public void The_question_carries_no_second_line()
    {
        AutomationSetting setting = PriceMaintenanceCatalog.Describe(maintaining: true).Single();

        Assert.Equal(string.Empty, setting.Summary);
        Assert.All(PriceMaintenanceCatalog.Rows(setting), row => Assert.Equal(string.Empty, row.Hint));
    }

    /// <summary>
    /// The write is a server RPC; a guest has nothing to write with. See
    /// <c>ListedPriceWriter</c>, which carries the reading.
    /// </summary>
    [Fact]
    public void It_only_does_anything_on_the_host()
    {
        Assert.True(PriceMaintenanceCatalog.Describe(maintaining: true).Single().HostOnly);
    }

    // ---- ticket 51: what a failed reading is able to say -------------------

    /// <summary>
    /// A reading the game broke off still names its product.
    /// </summary>
    /// <remarks>
    /// It did not, and that is why four of the owner's sessions each recorded a
    /// single line reading "<c>: the game broke off the reading …</c>" with
    /// nothing before the colon, for a failure that happened on every product of
    /// every pass. <c>DebugRecorder</c> keys a listed-price row on the product
    /// id (<c>DebugRow.Subject</c>), so four anonymous products were one subject
    /// and three of the four rows were dropped as "not news".
    /// </remarks>
    [Fact]
    public void A_reading_the_game_broke_off_still_names_its_product()
    {
        ProductValuation broken = ProductValuation.Unavailable(
            "the game broke off the reading (it went away)", "greencrack", "Green Crack");

        Assert.False(broken.Available);
        Assert.Equal("greencrack", broken.ProductId);
        Assert.Equal("Green Crack", broken.ProductName);
    }

    /// <summary>
    /// A customer the reading had to step over is carried into the sentence the
    /// log and the debug record both get, because it is the only place the
    /// refused figure is ever written down.
    /// </summary>
    [Fact]
    public void What_the_reading_stepped_over_rides_with_the_decision()
    {
        var valuation = new ProductValuation { Available = true, ProductName = "Green Crack" };

        Assert.Equal("Green Crack: already at $40", valuation.Explaining("Green Crack: already at $40"));

        valuation.Notes.Add("Beth Penn: the game answered with an order of 2149634 units");

        Assert.Equal(
            "Green Crack: already at $40 — Beth Penn: the game answered with an order of 2149634 units",
            valuation.Explaining("Green Crack: already at $40"));
    }

    /// <summary>
    /// A whole roster refused for the same reason is one fact repeated forty
    /// times. Three of them go in the sentence and the rest are counted, so the
    /// log line and the <c>decisions.jsonl</c> row stay ones a person finishes.
    /// </summary>
    [Fact]
    public void A_roster_refused_for_the_same_reason_is_counted_rather_than_listed()
    {
        var valuation = new ProductValuation { Available = true };

        for (int i = 1; i <= 40; i++)
        {
            valuation.Notes.Add($"Customer {i}: refused");
        }

        string explained = valuation.Explaining("priced");

        Assert.Equal(
            "priced — Customer 1: refused; Customer 2: refused; Customer 3: refused; "
            + "and 37 more like it",
            explained);
    }
}
