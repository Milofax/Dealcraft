using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The instrument the counteroffer investigation turns on.
///
/// <para>
/// Ticket 53: four negotiations in the owner's session, four skips, and a best
/// chance of <b>zero</b> at every price and every quantity for four customers
/// who had offered the contract themselves. The reason recorded was "no price at
/// any quantity reached the confidence the search asked for", which reads as a
/// threshold set too high and is not what happened. Nothing in the file could
/// tell a customer who says no from a probe that threw, because
/// <c>OfferChanceProbe.Chance</c> answered zero to both.
/// </para>
///
/// <para>
/// So these tests are about one question: does the record now say <em>which</em>
/// zero it was. Nothing here fixes anything, and that is the point — a repair
/// with no measurement behind it is how this has already been wrong twice.
/// </para>
/// </summary>
public class ProbeDiagnosisTests
{
    /// <summary>
    /// The mixed product from the owner's own session, spelled as the game
    /// spells it, because a diagnosis read back with an unfamiliar name in it is
    /// a diagnosis of something else.
    /// </summary>
    private const string Product = "triomit4fäusten";

    /// <summary>
    /// A whole sentence, pinned. Everything the ticket asks to be recorded is in
    /// this one line: what was handed in, what came back at the first price and
    /// the last, and whether anything threw.
    /// </summary>
    [Fact]
    public void One_search_comes_to_one_sentence_carrying_all_three_halves()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Offered(ProbeOffering.Of(Product, quantity: 5, amount: 20, quality: 4, packaging: "Jar"));
        diagnosis.Answered(5, 220f, 0f);
        diagnosis.Answered(5, 980f, 0f);

        Assert.Equal(
            $"handed in 5 × {Product} at 20 per package, quality Heavenly (4), packaged as Jar; "
            + "2 price points, first 5 for $220 answered 0, last 5 for $980 answered 0, "
            + "best anywhere 0; nothing threw",
            diagnosis.Summary());
    }

    // ---- what was handed in ------------------------------------------------

    /// <summary>
    /// The ticket's lead, and the reason this field exists at all.
    /// <c>GetOfferSuccessChance</c> reads
    /// <c>ProductItemInstance.get_AppliedPackaging</c> itself, so an instance
    /// handed over without one is a candidate answer — and it must be legible as
    /// an absence rather than as a blank.
    /// </summary>
    [Fact]
    public void An_instance_with_no_packaging_says_so_in_as_many_words()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Offered(ProbeOffering.Of(Product, quantity: 5, amount: 20, quality: 4, packaging: null));
        diagnosis.Answered(5, 220f, 0f);

        Assert.Contains("no packaging applied", diagnosis.Summary());
    }

    /// <summary>
    /// The other candidate answer: an empty item list. The game scores the
    /// goods, so a list with nothing in it is a deal with nothing in it, and
    /// zero would be the correct answer to it.
    /// </summary>
    [Fact]
    public void An_item_list_that_was_never_built_names_the_call_that_returned_null()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Offered(ProbeOffering.Nothing(Product, quantity: 5));
        diagnosis.Answered(5, 220f, 0f);

        string summary = diagnosis.Summary()!;

        Assert.Contains("GetDefaultInstance(5) returned null", summary);
        Assert.Contains("the item list was empty", summary);
    }

    /// <summary>
    /// And the third: an instance that is not a product instance, off which
    /// neither figure the customer scores can be read. Said plainly rather than
    /// reported as a quality of zero, which is Trash and is a real grade.
    /// </summary>
    [Fact]
    public void An_instance_that_is_not_a_product_is_not_reported_as_grade_zero()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Offered(ProbeOffering.Unreadable(Product, quantity: 5));
        diagnosis.Answered(5, 220f, 0f);

        string summary = diagnosis.Summary()!;

        Assert.Contains("not a product instance", summary);
        Assert.DoesNotContain("Trash", summary);
    }

    /// <summary>
    /// The quality is written as the game's name and as its number, the way the
    /// handover row writes it: the file is read without the binary to hand.
    /// </summary>
    [Theory]
    [InlineData(QualityTier.Trash, "Trash (0)")]
    [InlineData(QualityTier.Standard, "Standard (2)")]
    [InlineData(QualityTier.Heavenly, "Heavenly (4)")]
    public void The_quality_handed_in_is_written_as_a_name_and_a_rank(int quality, string expected)
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Offered(ProbeOffering.Of(Product, 5, 20, quality, "Jar"));
        diagnosis.Answered(5, 220f, 0f);

        Assert.Contains(expected, diagnosis.Summary());
    }

    /// <summary>
    /// One offering per attempt, however many quantities the search builds for.
    /// The definition, the quality and the packaging do not vary across a
    /// search; repeating them a dozen times would bury the rest of the line.
    /// </summary>
    [Fact]
    public void The_offering_is_recorded_once_however_often_the_items_are_rebuilt()
    {
        var diagnosis = new ProbeDiagnosis();

        for (int quantity = 1; quantity <= 12; quantity++)
        {
            diagnosis.Offered(ProbeOffering.Of(Product, quantity, 20, 4, "Jar"));
            diagnosis.Answered(quantity, 100f * quantity, 0f);
        }

        string summary = diagnosis.Summary()!;

        Assert.Equal(1, Occurrences(summary, "handed in"));
    }

    // ---- what came back ----------------------------------------------------

    /// <summary>
    /// The distinction the whole exercise is for: a flat zero and a curve that
    /// rises and never clears are the same verdict today and different faults.
    /// The best answer anywhere is what tells them apart, so it is recorded
    /// beside the first and the last.
    /// </summary>
    [Fact]
    public void A_flat_zero_reads_differently_from_a_curve_that_never_clears()
    {
        var flat = new ProbeDiagnosis();
        var rises = new ProbeDiagnosis();

        foreach (float price in new[] { 220f, 600f, 980f })
        {
            flat.Answered(5, price, 0f);
        }

        rises.Answered(5, 220f, 0.44f);
        rises.Answered(5, 600f, 0.12f);
        rises.Answered(5, 980f, 0f);

        Assert.Contains("best anywhere 0;", flat.Summary());
        Assert.Contains("best anywhere 0.44;", rises.Summary());
        Assert.NotEqual(flat.Summary(), rises.Summary());
    }

    /// <summary>
    /// A chance is written as the game gave it and not as a percentage. 0.004
    /// rounds to 0% and the difference between 0.004 and nothing at all is
    /// exactly what is being measured.
    /// </summary>
    [Fact]
    public void A_chance_just_above_zero_is_not_rounded_down_to_it()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Answered(5, 220f, 0.004f);
        diagnosis.Answered(5, 980f, 0f);

        Assert.Contains("answered 0.004", diagnosis.Summary());
    }

    /// <summary>
    /// A chance over one stays over one, for the reason the row's own chance
    /// does: the game's figure can exceed its own range and the one file that
    /// records what was found must not be where that is lost.
    /// </summary>
    [Fact]
    public void A_chance_over_one_is_recorded_as_it_was_given()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Answered(5, 220f, 1.01f);

        Assert.Contains("answered 1.01", diagnosis.Summary());
    }

    /// <summary>
    /// A single price point is not reported as a first and a last of itself.
    /// </summary>
    [Fact]
    public void One_price_point_is_reported_as_one()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Answered(5, 220f, 0.5f);

        Assert.Contains("1 price point, 5 for $220 answered 0.5", diagnosis.Summary());
    }

    // ---- what went wrong ---------------------------------------------------

    /// <summary>
    /// The hypothesis the ticket names, and what the record would say if it were
    /// right: every probe threw, so every chance was zero, and the type and
    /// message are now in the file instead of nowhere.
    /// </summary>
    [Fact]
    public void Every_probe_throwing_is_recorded_with_its_type_and_message()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Offered(ProbeOffering.Of(Product, 5, 20, 4, "Jar"));

        for (int probe = 0; probe < 26; probe++)
        {
            diagnosis.Failed(
                nameof(InvalidOperationException),
                "Unable to cast object of the type Il2CppSystem.Collections.Generic.List`1");
        }

        string summary = diagnosis.Summary()!;

        Assert.Contains("no price point answered", summary);
        Assert.Contains("26 of 26 probes failed", summary);
        Assert.Contains("first InvalidOperationException: Unable to cast object", summary);
    }

    /// <summary>
    /// And once, not twenty-six times. A bisection asks a dozen times inside a
    /// sweep that runs every few seconds, and a failure written per call is a
    /// log nobody can read.
    /// </summary>
    [Fact]
    public void The_same_failure_a_dozen_times_is_written_down_once()
    {
        var diagnosis = new ProbeDiagnosis();

        for (int probe = 0; probe < 12; probe++)
        {
            diagnosis.Failed(nameof(NullReferenceException), "object reference not set");
        }

        Assert.Equal(1, Occurrences(diagnosis.Summary()!, "object reference not set"));
    }

    /// <summary>
    /// The first failure is the one kept. The hundredth is the same one, and the
    /// first is the one with the search still in a known state behind it.
    /// </summary>
    [Fact]
    public void The_first_failure_is_the_one_kept()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Failed(nameof(NullReferenceException), "the first thing that went wrong");
        diagnosis.Failed(nameof(InvalidOperationException), "a later one");

        string summary = diagnosis.Summary()!;

        Assert.Contains("the first thing that went wrong", summary);
        Assert.DoesNotContain("a later one", summary);
    }

    /// <summary>
    /// A customer or a product that has gone is not an exception and must not
    /// read as one: the probe never reached the game, and a reader hunting an
    /// interop fault would waste a session on it.
    /// </summary>
    [Fact]
    public void A_probe_that_never_reached_the_game_is_not_spelled_as_a_throw()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Failed(null, "the customer or the product had gone before the probe ran");

        string summary = diagnosis.Summary()!;

        Assert.Contains("first the customer or the product had gone", summary);
        Assert.DoesNotContain("Exception", summary);
    }

    /// <summary>
    /// Both counts are kept, so a search where some probes answered and some
    /// threw is legible as exactly that rather than as either one.
    /// </summary>
    [Fact]
    public void A_search_that_half_failed_says_how_many_of_how_many()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Answered(5, 220f, 0.2f);
        diagnosis.Answered(5, 400f, 0.1f);
        diagnosis.Failed(nameof(NullReferenceException), "object reference not set");

        Assert.Contains("2 price points", diagnosis.Summary());
        Assert.Contains("1 of 3 probes failed", diagnosis.Summary());
    }

    /// <summary>
    /// A message long enough to bury the line is cut. The game's interop
    /// messages can carry a type name per argument, and one row has to stay one
    /// readable line.
    /// </summary>
    [Fact]
    public void A_very_long_message_is_shortened_rather_than_carried_whole()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Failed("VerificationException", new string('x', 4000));

        string summary = diagnosis.Summary()!;

        Assert.Contains("VerificationException", summary);
        Assert.DoesNotContain(new string('x', ProbeDiagnosis.MostOfAMessage + 1), summary);
    }

    /// <summary>
    /// A failure with nothing said about it is still counted, and the count is
    /// the fact. A line trailing off after "first" would be the one line in the
    /// file that says nothing.
    /// </summary>
    [Fact]
    public void A_failure_with_neither_a_type_nor_a_message_is_still_counted()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Failed(null, "   ");

        Assert.Contains("1 of 1 probes failed, with nothing said about why", diagnosis.Summary());
    }

    /// <summary>
    /// And a later failure that does say something is kept in its place. The
    /// point of keeping the first is that the hundredth is the same one; a first
    /// that says nothing is not the same one.
    /// </summary>
    [Fact]
    public void A_silent_failure_gives_way_to_a_later_one_that_says_something()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Failed(null, null);
        diagnosis.Failed(nameof(NullReferenceException), "object reference not set");

        Assert.Contains(
            "2 of 2 probes failed, first NullReferenceException: object reference not set",
            diagnosis.Summary());
    }

    // ---- the silence -------------------------------------------------------

    /// <summary>
    /// A search that stopped before it asked anything says nothing at all. The
    /// row then carries a null, which is the same thing it does with the chance
    /// for those rows: "we never asked" is not a measurement.
    /// </summary>
    [Fact]
    public void A_search_that_asked_nothing_has_nothing_to_say()
    {
        Assert.Null(new ProbeDiagnosis().Summary());
    }

    // ---- it reaches the file -----------------------------------------------

    /// <summary>
    /// The whole point: it lands in <c>decisions.jsonl</c>, on the negotiation
    /// row that already records the skip, and it parses.
    /// </summary>
    [Fact]
    public void The_diagnosis_reaches_the_negotiation_row_it_explains()
    {
        var diagnosis = new ProbeDiagnosis();

        diagnosis.Offered(ProbeOffering.Of(Product, 5, 20, 4, packaging: null));
        diagnosis.Answered(5, 220f, 0f);
        diagnosis.Answered(5, 980f, 0f);

        DebugRow row = DebugRow.Negotiation(
            "Skip",
            acted: false,
            reason: "no price at any quantity reached the confidence the search asked for",
            customerName: "Mrs. Ming",
            contractKey: "contract-1",
            productId: Product,
            chance: 0f,
            offeredPayment: 200f,
            probe: diagnosis.Summary());

        JsonElement written = JsonDocument.Parse(row.ToJson()).RootElement;

        Assert.Equal(diagnosis.Summary(), written.GetProperty("probe").GetString());
        Assert.Equal(0f, written.GetProperty("chance").GetSingle());
    }

    /// <summary>
    /// And a row with no search behind it carries the key with a null in it, the
    /// way every other absent figure on that row is carried: a reader filtering
    /// on <c>acted == false</c> gets rows that look finished.
    /// </summary>
    [Fact]
    public void A_row_with_no_probe_behind_it_carries_the_key_empty()
    {
        JsonElement written = JsonDocument
            .Parse(DebugRow.Negotiation(
                "Skip", acted: false, reason: "a dealer is handling this customer",
                customerName: "Mrs. Ming", contractKey: "contract-1").ToJson())
            .RootElement;

        Assert.Equal(JsonValueKind.Null, written.GetProperty("probe").ValueKind);
    }

    /// <summary>
    /// A machine whose decimal separator is a comma writes the same figures.
    /// The owner's is one, and the file he sends back has to parse here.
    /// </summary>
    [Fact]
    public void A_machine_with_a_comma_for_a_decimal_point_writes_the_same_line()
    {
        CultureInfo was = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            var diagnosis = new ProbeDiagnosis();

            diagnosis.Offered(ProbeOffering.Of(Product, 5, 20, 4, "Jar"));
            diagnosis.Answered(5, 220.5f, 0.125f);

            Assert.Contains("5 for $220.5 answered 0.125", diagnosis.Summary());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = was;
        }
    }

    /// <summary>
    /// The same diagnosis about the same contract is still one line in the file.
    /// The record deduplicates on the row's digest and the probe's account is
    /// part of it, so a sweep that reaches the same verdict twice costs one row
    /// — and one whose probe started failing costs a second, which is news.
    /// </summary>
    [Fact]
    public void The_record_still_writes_a_repeated_verdict_once_and_a_changed_probe_again()
    {
        var lines = 0;
        var recorder = new DebugRecorder(_ =>
        {
            lines++;
            return true;
        });

        DebugRow Row(string probe) => DebugRow.Negotiation(
            "Skip", acted: false, reason: "no price at any quantity reached the confidence",
            customerName: "Mrs. Ming", contractKey: "contract-1", chance: 0f, probe: probe);

        recorder.Record(Row("2 price points, first 5 for $220 answered 0"));
        recorder.Record(Row("2 price points, first 5 for $220 answered 0"));
        Assert.Equal(1, lines);

        recorder.Record(Row("no price point answered; 26 of 26 probes failed"));
        Assert.Equal(2, lines);
    }

    // ---- the far side of the seam ------------------------------------------

    /// <summary>
    /// The call itself is in the Il2Cpp adapter where no test may follow it, so
    /// the rule is checked where it can be: the probe's catch still answers
    /// zero, and it no longer answers it in silence.
    /// </summary>
    /// <remarks>
    /// Both halves matter. Writing the exception down is what this ticket is
    /// for; still answering zero is what stops it becoming a repair — the search
    /// must not be handed a special value to reason about, and a probe that
    /// threw is not evidence the customer would have said yes.
    /// </remarks>
    [Fact]
    public void The_adapters_swallowed_exception_is_written_down_and_still_answers_zero()
    {
        string source = AdapterSource.Read("OfferChanceProbe.cs");

        Assert.Contains("catch (Exception error)", source);

        // The message alone was not enough to read the owner's session by: an
        // interop failure says "Object reference not set to an instance of an
        // object" and nothing about which call it was. Said() walks to the
        // innermost exception and appends the frame that threw.
        Assert.Contains("Diagnosis.Failed(error.GetType().Name, Said(error));", source);
        Assert.Contains("private static string Said(Exception trouble)", source);

        Assert.Contains("return 0f;", source);
    }

    private static int Occurrences(string text, string what)
    {
        int found = 0;

        for (int at = text.IndexOf(what, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
        {
            found++;
        }

        return found;
    }
}
