using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The debug record, tested the way <c>bin/read-ledger --self-test</c> tests the
/// ledger: rows are written, read back with a real JSON parser, and asserted.
///
/// <para>
/// Reading back rather than asserting substrings is the whole point. The file
/// exists to be read by whoever is debugging, a session later, with nothing to
/// hand but <c>jq</c>; a test that only checked the mod's own spelling of a row
/// would pass on a line no parser accepts.
/// </para>
///
/// <para>
/// The unhappy rows get the most of the attention here, because they are the
/// ones the ticket is about: a session where nothing happened, a refusal that
/// arrives with no reason, and a sink that has nothing to write to yet.
/// </para>
/// </summary>
public class DebugRecordTests
{
    /// <summary>
    /// A recorder writing into a list, which is every test here but the one
    /// that goes to a real file.
    /// </summary>
    private sealed class Sink
    {
        private readonly List<string> _lines = new();

        public bool Refusing { get; set; }

        public IReadOnlyList<string> Lines => _lines;

        public bool Append(string line)
        {
            if (Refusing)
            {
                return false;
            }

            _lines.Add(line);
            return true;
        }

        /// <summary>One line, parsed. Fails loudly rather than returning null.</summary>
        public JsonElement Row(int index) => JsonDocument.Parse(_lines[index]).RootElement;
    }

    private static (DebugRecorder Recorder, Sink Sink) Recording()
    {
        var sink = new Sink();
        return (new DebugRecorder(sink.Append), sink);
    }

    // ---- the happy rows ----------------------------------------------------

    /// <summary>
    /// A counter-offer that went out, with everything the ticket asks a
    /// negotiation row to carry: customer, product, quantity, price, chance and
    /// what was sent.
    /// </summary>
    [Fact]
    public void A_counteroffer_that_was_sent_carries_what_was_sent_and_why()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(DebugRow.Negotiation(
            outcome: nameof(CounterofferOutcome.Send),
            acted: true,
            reason: "countering beats the offer on the table",
            customerName: "Jessi Waters",
            contractKey: "contract-1",
            productId: "greencrack",
            quantity: 6,
            price: 480f,
            chance: 0.92f,
            offeredPayment: 300f));

        JsonElement row = sink.Row(0);

        Assert.Equal("negotiation", row.GetProperty("decision").GetString());
        Assert.Equal("Send", row.GetProperty("outcome").GetString());
        Assert.True(row.GetProperty("acted").GetBoolean());
        Assert.Equal("countering beats the offer on the table", row.GetProperty("reason").GetString());
        Assert.Equal("Jessi Waters", row.GetProperty("customer").GetString());
        Assert.Equal("contract-1", row.GetProperty("contract").GetString());
        Assert.Equal("greencrack", row.GetProperty("product").GetString());
        Assert.Equal(6, row.GetProperty("quantity").GetInt32());
        Assert.Equal(480f, row.GetProperty("price").GetSingle());
        Assert.Equal(0.92f, row.GetProperty("chance").GetSingle());
        Assert.Equal(300f, row.GetProperty("offered_payment").GetSingle());
    }

    /// <summary>
    /// The window a deal went into, by the game's own name for it, so a reader
    /// can line the row up against what the game drew.
    /// </summary>
    [Fact]
    public void A_deal_that_was_scheduled_names_its_window()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(DebugRow.Scheduling(
            outcome: nameof(ScheduleOutcome.Schedule),
            acted: true,
            reason: "the first window you allow that has room",
            customerName: "Jessi Waters",
            contractKey: "contract-1",
            window: DealWindowName.Of(DealWindow.Afternoon)));

        JsonElement row = sink.Row(0);

        Assert.Equal("scheduling", row.GetProperty("decision").GetString());
        Assert.Equal(DealWindowName.Of(DealWindow.Afternoon), row.GetProperty("window").GetString());
        Assert.True(row.GetProperty("acted").GetBoolean());
    }

    /// <summary>
    /// A completed handover: grade, packages, units and the money it is for.
    /// The grade is written twice — as the game's number and as the game's
    /// name — because the file is read without the binary to hand.
    /// </summary>
    [Fact]
    public void A_completed_handover_carries_the_grade_the_packages_the_units_and_the_money()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(DebugRow.Handover(
            outcome: nameof(HandoverAction.HandOver),
            acted: true,
            reason: "Heavenly is the lowest grade in reach that meets the contract's Premium",
            customerName: "Jessi Waters",
            contractKey: "contract-1",
            productId: "greencrack",
            grade: QualityTier.Highest,
            packages: 2,
            units: 10,
            contractPayment: 1000f));

        JsonElement row = sink.Row(0);

        Assert.Equal("handover", row.GetProperty("decision").GetString());
        Assert.Equal(QualityTier.Highest, row.GetProperty("grade").GetInt32());
        Assert.Equal(QualityTier.Name(QualityTier.Highest), row.GetProperty("grade_name").GetString());
        Assert.Equal(2, row.GetProperty("packages").GetInt32());
        Assert.Equal(10, row.GetProperty("units").GetInt32());
        Assert.Equal(1000f, row.GetProperty("contract_payment").GetSingle());
    }

    /// <summary>
    /// The listed price is the one the app no longer shows anywhere:
    /// <c>app.md</c> records that deleting the <c>Products</c> tab leaves a
    /// price Dealcraft wrote visible nowhere in the game. So the row carries
    /// both sides of the move.
    /// </summary>
    [Fact]
    public void A_listed_price_write_carries_the_old_price_and_the_new_one()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(DebugRow.ListedPrice(
            outcome: nameof(ListedPriceOutcome.Write),
            acted: true,
            reason: "Green Crack: $40 becomes $52, which 9 of your customers still clear",
            productId: "greencrack",
            oldPrice: 40f,
            newPrice: 52f));

        JsonElement row = sink.Row(0);

        Assert.Equal("listed_price", row.GetProperty("decision").GetString());
        Assert.Equal("greencrack", row.GetProperty("product").GetString());
        Assert.Equal(40f, row.GetProperty("old_price").GetSingle());
        Assert.Equal(52f, row.GetProperty("new_price").GetSingle());
    }

    // ---- the unhappy rows, which are the ones that matter ------------------

    /// <summary>
    /// The ticket's first demand: a refusal is recorded as fully as an action.
    /// The same keys, in the same order, with <c>null</c> where there is
    /// nothing — so a reader filtering on <c>acted == false</c> gets rows that
    /// look finished rather than rows that look half-written.
    /// </summary>
    [Fact]
    public void A_refusal_carries_every_key_an_action_carries()
    {
        (DebugRecorder acting, Sink actions) = Recording();
        (DebugRecorder refusing, Sink refusals) = Recording();

        acting.Record(DebugRow.Negotiation(
            "Send", acted: true, reason: "worth more than the money on the table",
            customerName: "Jessi Waters", contractKey: "contract-1",
            productId: "greencrack", quantity: 6, price: 480f, chance: 0.92f, offeredPayment: 300f));

        refusing.Record(DebugRow.Negotiation(
            "Skip", acted: false, reason: "a dealer is handling this customer",
            customerName: "Jessi Waters", contractKey: "contract-1"));

        Assert.Equal(KeysOf(actions.Row(0)), KeysOf(refusals.Row(0)));

        JsonElement refusal = refusals.Row(0);
        Assert.False(refusal.GetProperty("acted").GetBoolean());
        Assert.Equal("a dealer is handling this customer", refusal.GetProperty("reason").GetString());

        // Present and explicitly empty, which is the ledger's own rule: a
        // reader can tell "there was nothing to say" from "this version did
        // not write it".
        foreach (string key in new[] { "product", "quantity", "price", "chance", "offered_payment", "probe" })
        {
            Assert.Equal(JsonValueKind.Null, refusal.GetProperty(key).ValueKind);
        }
    }

    /// <summary>
    /// A refusal that arrives with no reason still gets one. The gates all
    /// populate theirs — <c>CounterofferDecision.Reason</c> answers "nothing was
    /// decided" for a default struct — but a row whose reason is an empty string
    /// would be the one line in the file that says nothing, and the file exists
    /// for the reasons.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_refusal_with_no_reason_is_given_one(string? nothing)
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(DebugRow.Handover(
            "Refuse", acted: false, reason: nothing!,
            customerName: "Jessi Waters", contractKey: "contract-1"));

        string? reason = sink.Row(0).GetProperty("reason").GetString();

        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.Equal("no reason was given", reason);
    }

    /// <summary>
    /// Each of the three silences the spec names, carried as the sentence its
    /// own search wrote. Nothing here re-words them and nothing here counts
    /// them: the reason is the gate's and the reader reads it.
    /// </summary>
    [Theory]
    [InlineData("there is no Green Crack in reach at all")]
    [InlineData("Premium has 6 units in reach and no 4 packages of it come to the 10 the contract asks for")]
    [InlineData("nothing at or above Premium is in reach")]
    public void Each_of_the_three_silences_reaches_the_file_in_its_own_words(string silence)
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(DebugRow.Handover(
            "Refuse", acted: false, reason: silence,
            customerName: "Jessi Waters", contractKey: "contract-1", productId: "greencrack"));

        JsonElement row = sink.Row(0);

        Assert.Equal(silence, row.GetProperty("reason").GetString());
        Assert.False(row.GetProperty("acted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("grade").ValueKind);
    }

    /// <summary>
    /// A session where nothing happened writes nothing. Not an empty object,
    /// not a heading, not a line saying the session began — with the record on
    /// by default, a player who never switches an automation on must pay
    /// nothing for it, and a file that does not exist is the cheapest possible
    /// answer.
    /// </summary>
    [Fact]
    public void A_session_where_nothing_happened_writes_nothing_at_all()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        Assert.Empty(sink.Lines);
        Assert.Equal(0, recorder.Written);
        Assert.False(recorder.Stopped);
    }

    /// <summary>
    /// And the file it would write to does not have to exist first. The debug
    /// file is found the way the ledger is found and made the same way: the
    /// first row creates it. Here that is the sink's own business, which is the
    /// point — the recorder never resolves a path.
    /// </summary>
    [Fact]
    public void The_first_row_is_written_to_a_file_that_did_not_exist_yet()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "dealcraft-debug-" + Guid.NewGuid().ToString("N") + ".jsonl");

        Assert.False(File.Exists(path));

        try
        {
            var recorder = new DebugRecorder(line =>
            {
                File.AppendAllText(path, line + "\n");
                return true;
            });

            recorder.Record(DebugRow.Negotiation(
                "Skip", acted: false, reason: "you have not switched negotiation on",
                customerName: "Jessi Waters", contractKey: "contract-1"));

            string[] lines = File.ReadAllLines(path);
            JsonElement row = JsonDocument.Parse(Assert.Single(lines)).RootElement;

            Assert.Equal("negotiation", row.GetProperty("decision").GetString());
            Assert.Equal(
                "you have not switched negotiation on", row.GetProperty("reason").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// A sink that cannot write closes the record for the session rather than
    /// being asked again on the next sweep. A disk that is full is a reason to
    /// stop recording; it is never a reason to spend a frame finding out again
    /// every two seconds.
    /// </summary>
    [Fact]
    public void A_sink_that_refuses_a_line_closes_the_record_for_the_session()
    {
        (DebugRecorder recorder, Sink sink) = Recording();
        sink.Refusing = true;

        Assert.False(recorder.Record(Skip("contract-1")));
        Assert.True(recorder.Stopped);

        sink.Refusing = false;
        Assert.False(recorder.Record(Skip("contract-2")));
        Assert.Empty(sink.Lines);
    }

    // ---- the bound ---------------------------------------------------------

    /// <summary>
    /// The bound that matters: a sweep that reaches the same verdict about the
    /// same contract every five seconds costs one line, not one a sweep.
    /// </summary>
    [Fact]
    public void The_same_decision_about_the_same_contract_is_written_once()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        for (int sweep = 0; sweep < 100; sweep++)
        {
            recorder.Record(Skip("contract-1"));
        }

        Assert.Single(sink.Lines);
    }

    /// <summary>
    /// And the clock does not count as a change. The row's digest leaves
    /// <c>recorded_at</c> out for exactly this: with it in, every row would
    /// differ from the last and the bound above would not exist.
    /// </summary>
    [Fact]
    public void A_later_clock_is_not_a_different_decision()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        DebugRow first = Skip("contract-1");
        first.RecordedAt = new DateTimeOffset(2026, 9, 18, 20, 0, 0, TimeSpan.Zero);

        DebugRow later = Skip("contract-1");
        later.RecordedAt = new DateTimeOffset(2026, 9, 18, 23, 59, 0, TimeSpan.Zero);

        recorder.Record(first);
        recorder.Record(later);

        Assert.Single(sink.Lines);
    }

    /// <summary>
    /// A reason that changes is news again — which is the half of the rule that
    /// makes the file worth reading. "A dealer is handling this" becoming "the
    /// window has closed" is the sequence somebody debugging is looking for.
    /// </summary>
    [Fact]
    public void A_decision_that_changed_is_written_again()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(DebugRow.Negotiation(
            "Skip", acted: false, reason: "a dealer is handling this customer",
            customerName: "Jessi Waters", contractKey: "contract-1"));
        recorder.Record(DebugRow.Negotiation(
            "Skip", acted: false, reason: "the offer has gone",
            customerName: "Jessi Waters", contractKey: "contract-1"));

        Assert.Equal(2, sink.Lines.Count);
        Assert.Equal("a dealer is handling this customer", sink.Row(0).GetProperty("reason").GetString());
        Assert.Equal("the offer has gone", sink.Row(1).GetProperty("reason").GetString());
    }

    /// <summary>
    /// Two contracts refused for the same reason are two rows. The memory is
    /// per subject, so one customer's silence never speaks for another's.
    /// </summary>
    [Fact]
    public void Two_contracts_refused_for_the_same_reason_are_two_rows()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(Skip("contract-1"));
        recorder.Record(Skip("contract-2"));

        Assert.Equal(2, sink.Lines.Count);
    }

    /// <summary>
    /// The same sentence about the same contract from two different passes is
    /// still two rows: the memory is keyed on the kind of decision as well as
    /// the subject, so a scheduling verdict never silences a handover one.
    /// </summary>
    [Fact]
    public void Two_passes_saying_the_same_thing_about_one_contract_are_two_rows()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(DebugRow.Scheduling(
            "Skip", acted: false, reason: "you are leaving this customer alone",
            customerName: "Jessi Waters", contractKey: "contract-1"));
        recorder.Record(DebugRow.Handover(
            "Abstain", acted: false, reason: "you are leaving this customer alone",
            customerName: "Jessi Waters", contractKey: "contract-1"));

        Assert.Equal(2, sink.Lines.Count);
    }

    /// <summary>
    /// A reloaded save is news again. The passes forget their own log memory
    /// when the scene changes, because the contracts are new objects and the
    /// reasons are worth hearing against the new session; the record does the
    /// same.
    /// </summary>
    [Fact]
    public void Forgetting_makes_a_standing_reason_news_again()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(Skip("contract-1"));
        recorder.Forget();
        recorder.Record(Skip("contract-1"));

        Assert.Equal(2, sink.Lines.Count);
    }

    /// <summary>
    /// The ceiling, stated and held: a session writes at most
    /// <see cref="DebugRecorder.MostRowsInOneSession"/> decisions, and then one
    /// row saying that is why the file ends. A reader who finds a short file
    /// has to be able to tell a quiet evening from a full one.
    /// </summary>
    [Fact]
    public void The_record_stops_at_its_ceiling_and_says_that_it_did()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        for (int i = 0; i < DebugRecorder.MostRowsInOneSession + 50; i++)
        {
            recorder.Record(Skip("contract-" + i.ToString(CultureInfo.InvariantCulture)));
        }

        Assert.True(recorder.Stopped);
        Assert.Equal(DebugRecorder.MostRowsInOneSession + 1, sink.Lines.Count);

        JsonElement last = sink.Row(sink.Lines.Count - 1);
        Assert.Equal("record", last.GetProperty("decision").GetString());
        Assert.Equal("Stopped", last.GetProperty("outcome").GetString());
        Assert.Contains("restarted", last.GetProperty("reason").GetString());
    }

    /// <summary>
    /// And memory stays flat while it does. A session that meets more subjects
    /// than the record remembers forgets them and says so, so that rows
    /// repeating themselves further down the file are explained rather than
    /// mysterious.
    /// </summary>
    [Fact]
    public void Meeting_more_subjects_than_it_remembers_is_said_out_loud()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        for (int i = 0; i <= DebugRecorder.MostSubjectsRemembered; i++)
        {
            recorder.Record(Skip("contract-" + i.ToString(CultureInfo.InvariantCulture)));
        }

        var notes = new List<JsonElement>();
        for (int i = 0; i < sink.Lines.Count; i++)
        {
            JsonElement row = sink.Row(i);
            if (row.GetProperty("decision").GetString() == "record")
            {
                notes.Add(row);
            }
        }

        JsonElement note = Assert.Single(notes);
        Assert.Equal("Forgot", note.GetProperty("outcome").GetString());
        Assert.False(note.GetProperty("acted").GetBoolean());
    }

    // ---- the file is readable, whatever the machine ------------------------

    /// <summary>
    /// One row is one line. A reason carrying a quotation mark or a newline —
    /// and the game's own strings can — must not become two lines, because the
    /// file is read a line at a time.
    /// </summary>
    [Fact]
    public void A_reason_with_a_quote_or_a_newline_is_still_one_line()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        const string awkward = "the game said \"no\"\nand then stopped\ttalking";

        recorder.Record(DebugRow.Handover(
            "Refuse", acted: false, reason: awkward,
            customerName: "Jessi \"Jess\" Waters", contractKey: "contract-1"));

        string line = Assert.Single(sink.Lines);
        Assert.DoesNotContain("\n", line);

        JsonElement row = JsonDocument.Parse(line).RootElement;
        Assert.Equal(awkward, row.GetProperty("reason").GetString());
        Assert.Equal("Jessi \"Jess\" Waters", row.GetProperty("customer").GetString());
    }

    /// <summary>
    /// A machine whose decimal separator is a comma still writes a file
    /// <c>jq</c> can parse. The same rule <see cref="JsonObject"/> was written
    /// for, checked here because the debug file is the one a German host is
    /// most likely to be asked for.
    /// </summary>
    [Fact]
    public void A_machine_with_a_comma_for_a_decimal_point_still_writes_json()
    {
        CultureInfo was = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            (DebugRecorder recorder, Sink sink) = Recording();

            recorder.Record(DebugRow.ListedPrice(
                "Write", acted: true, reason: "Green Crack: $40.50 becomes $52.25",
                productId: "greencrack", oldPrice: 40.5f, newPrice: 52.25f));

            JsonElement row = sink.Row(0);

            Assert.Equal(40.5f, row.GetProperty("old_price").GetSingle());
            Assert.Equal(52.25f, row.GetProperty("new_price").GetSingle());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = was;
        }
    }

    /// <summary>
    /// A chance above 1 stays above 1. The game shows chances over 100% and
    /// <c>app.md</c> turns on being able to tell 101% from 100%; the one file
    /// that records what the search found must not be where that is lost.
    /// </summary>
    [Fact]
    public void A_chance_over_one_is_recorded_as_it_was_found()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(DebugRow.Negotiation(
            "Send", acted: true, reason: "the best rung the search found",
            customerName: "Jessi Waters", contractKey: "contract-1",
            productId: "greencrack", quantity: 6, price: 480f, chance: 1.01f));

        Assert.Equal(1.01f, sink.Row(0).GetProperty("chance").GetSingle());
    }

    /// <summary>
    /// Every row carries a timestamp that parses, so a row can be tied back to
    /// a moment in a session and put beside a line of <c>Latest.log</c>.
    /// </summary>
    [Fact]
    public void Every_row_carries_a_time_that_parses()
    {
        (DebugRecorder recorder, Sink sink) = Recording();

        recorder.Record(Skip("contract-1"));

        Assert.True(DateTimeOffset.TryParse(
            sink.Row(0).GetProperty("recorded_at").GetString(),
            CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out DateTimeOffset _));
    }

    /// <summary>
    /// The keys of each kind of row, written out, because they are read by
    /// something this suite cannot run.
    /// </summary>
    /// <remarks>
    /// <c>bin/read-ledger</c> reads this file with its own embedded copy of
    /// these rows and its own <c>--self-test</c> over them. Two halves that can
    /// drift, and the drift would show up as a tool that quietly stopped
    /// explaining a column. So the mod's half is pinned here: renaming or
    /// dropping a key fails this test, which names the tool.
    /// </remarks>
    [Theory]
    [InlineData("negotiation", "recorded_at,decision,outcome,acted,reason,customer,contract,"
        + "product,quantity,price,chance,offered_payment,probe")]
    [InlineData("scheduling", "recorded_at,decision,outcome,acted,reason,customer,contract,window")]
    [InlineData("handover", "recorded_at,decision,outcome,acted,reason,customer,contract,product,"
        + "grade,grade_name,packages,units,contract_payment")]
    [InlineData("listed_price", "recorded_at,decision,outcome,acted,reason,product,old_price,new_price")]
    [InlineData("record", "recorded_at,decision,outcome,acted,reason")]
    public void Each_kind_of_row_writes_the_keys_bin_read_ledger_reads(string kind, string keys)
    {
        DebugRow row = kind switch
        {
            "negotiation" => DebugRow.Negotiation("Skip", false, "why", "who", "what"),
            "scheduling" => DebugRow.Scheduling("Skip", false, "why", "who", "what"),
            "handover" => DebugRow.Handover("Wait", false, "why", "who", "what"),
            "listed_price" => DebugRow.ListedPrice("AlreadyThere", false, "why", "what"),
            _ => DebugRow.AboutTheRecord("Stopped", "why"),
        };

        JsonElement written = JsonDocument.Parse(row.ToJson()).RootElement;

        Assert.Equal(kind, written.GetProperty("decision").GetString());
        Assert.Equal(keys.Split(','), KeysOf(written));
    }

    private static DebugRow Skip(string contractKey) => DebugRow.Negotiation(
        "Skip",
        acted: false,
        reason: "a dealer is handling this customer",
        customerName: "Jessi Waters",
        contractKey: contractKey);

    private static IEnumerable<string> KeysOf(JsonElement row)
    {
        foreach (JsonProperty property in row.EnumerateObject())
        {
            yield return property.Name;
        }
    }
}
