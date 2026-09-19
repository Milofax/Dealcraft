using System;
using System.Collections.Generic;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The page, as <c>app.md</c> draws it: four blocks, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// One test per story that names a control, driven through <c>Build</c>, which
/// is the only seam these need — the page is a pure function from the
/// preferences file to the rows on screen, so every story is a test that runs
/// without the game.
/// </para>
/// <para>
/// Three of the four blocks are one question with two answers rather than a tick
/// box, because the off state is a policy: <em>use the game's own behaviour</em>,
/// which a cleared checkbox does not say. The fourth is four windows and no
/// switch above them.
/// </para>
/// </remarks>
public class AutomationFormTests
{
    /// <summary>
    /// Story 2: four blocks and nothing else, in the order the drawing puts
    /// them. Story 5 comes with it — there is no tab strip, because there is one
    /// page.
    /// </summary>
    [Fact]
    public void The_page_is_four_blocks_in_the_order_the_drawing_puts_them()
    {
        Assert.Equal(
            new[] { "LISTED PRICE", "PRICE NEGOTIATION", "ACCEPTED SCHEDULE", "HANDOVER" },
            Form(new AdvisorSettings()).Blocks.Select(block => block.Title));
    }

    /// <summary>
    /// Story 41: a deleted block that comes back fails here, named so that a
    /// later worker who thinks the page looks unfinished reads why it is short
    /// before restoring one.
    /// </summary>
    /// <remarks>
    /// <c>SAFETY</c> asked the owner to decide what runs cleanly while the game
    /// saves; <c>LAST HANDOVER</c> broke down a payment he had already been
    /// paid; <c>ACCEPT OFFERS</c> put a switch above four windows that already
    /// say which windows are allowed; and the footer showed a list of customers
    /// he sets on the customer.
    /// </remarks>
    [Fact]
    public void None_of_the_blocks_he_deleted_is_on_the_page()
    {
        string[] gone =
        {
            "SAFETY", "MY PRICES", "COUNTER-OFFERS", "ACCEPT OFFERS", "LAST HANDOVER",
        };

        IEnumerable<string> titles = Form(AllOn(), Everything(), Hours(), maintainPrices: true)
            .Blocks.Select(block => block.Title);

        Assert.Empty(titles.Intersect(gone, StringComparer.Ordinal));
    }

    /// <summary>
    /// Story 28 in both directions: every key the preferences file creates is a
    /// control the page draws, and every control the page draws is a key. One
    /// test over both lists, asserted whole, so neither can grow alone.
    /// </summary>
    /// <remarks>
    /// The file's side is <see cref="PreferencesFile.EntryNames"/> — the adapter's
    /// own <c>CreateEntry</c> calls — rather than the catalogue rows this test
    /// file builds for itself. Against the catalogues it was a check that the
    /// page draws what the catalogues describe, which is a much smaller claim
    /// than the one the doc comment makes, and it could not have caught a key
    /// declared in the adapter and described by nobody.
    /// </remarks>
    [Fact]
    public void Every_setting_is_either_drawn_on_the_page_or_named_as_the_one_that_is_not()
    {
        IReadOnlyList<AutomationSetting> settings = AllSettings(new AdvisorSettings());

        Assert.Equal(
            PreferencesFile.EntryNames().OrderBy(key => key, StringComparer.Ordinal),
            AutomationForm.Build(settings, ServerAuthority.Held).Claimed
                .Concat(AutomationForm.FileOnly)
                .OrderBy(key => key, StringComparer.Ordinal));
    }

    /// <summary>
    /// And the page's side of that, written out: nine controls, which is every
    /// key the file holds but the handover ledger.
    /// </summary>
    [Fact]
    public void The_page_draws_every_control_by_name()
    {
        Assert.Equal(
            new[]
            {
                "AcceptanceProbabilityThreshold",
                "AllowAfternoonWindow",
                "AllowLateNightWindow",
                "AllowMorningWindow",
                "AllowNightWindow",
                "AutoCounterOffer",
                "AutoHandover",
                "HandoverFromAnywhere",
                "MaintainListedPrices",
                "MayUseAHigherGrade",
            },
            Form(new AdvisorSettings()).Claimed.OrderBy(key => key, StringComparer.Ordinal));
    }

    /// <summary>
    /// Story 28, and the one name that is left on it. The acceptance threshold
    /// used to be here too and is drawn now as the chance floor under the
    /// negotiation block; the handover ledger stays, because the owner asked for
    /// it to be recorded and explicitly not surfaced. A second name appearing
    /// here is a setting that has gone missing from the app.
    /// </summary>
    [Fact]
    public void The_only_setting_left_out_of_the_page_is_the_handover_ledger()
    {
        Assert.Equal(new[] { "HandoverLedger" }, AutomationForm.FileOnly);
    }

    // --- every question has two answers --------------------------------------

    /// <summary>
    /// Stories 3 and 4. A fresh install answers every question with the game's
    /// own behaviour, and that answer is a ticked option rather than an empty
    /// box — so "not switched on" cannot be read as "broken".
    /// </summary>
    /// <remarks>
    /// The handover is not here: it is the one question with three answers, so
    /// it has a test of its own below rather than a row in this theory.
    /// </remarks>
    [Theory]
    [InlineData("LISTED PRICE", "MaintainListedPrices")]
    [InlineData("PRICE NEGOTIATION", "AutoCounterOffer")]
    public void A_fresh_install_answers_every_question_with_the_games_own_behaviour(
        string title, string key)
    {
        IReadOnlyList<FormRow> answers = Block(Form(new AdvisorSettings()), title).Rows;

        Assert.Equal(2, answers.Count);
        Assert.All(answers, row => Assert.Equal(FormRowKind.Choice, row.Kind));
        Assert.Equal(new[] { TwoAnswers.No(key), TwoAnswers.Yes(key) }, answers.Select(row => row.Id));

        Assert.Equal("Manual (game's default)", answers[0].Text);
        Assert.True(answers[0].Chosen);
        Assert.False(answers[1].Chosen);
    }

    /// <summary>
    /// Story 12: each <c>Automated</c> answer says what it automates, so that
    /// the two price blocks cannot be confused — as both of us confused them —
    /// without a second line of explanation beside the heading.
    /// </summary>
    [Fact]
    public void Each_automated_answer_says_what_it_automates()
    {
        AutomationForm form = Form(AllOn(), maintainPrices: true);

        Assert.Equal(
            "Automated setting in the Price App",
            Row(form, TwoAnswers.Yes("MaintainListedPrices")).Text);
        Assert.Equal(
            "Automated handling of customer requests",
            Row(form, TwoAnswers.Yes("AutoCounterOffer")).Text);
        Assert.Equal(
            "Automated handover when I talk to them",
            Row(form, TwoAnswers.Yes("AutoHandover")).Text);
    }

    /// <summary>
    /// Either answer writes the whole question, including the one already
    /// ticked. A radio whose selected option does nothing when pressed can be
    /// left showing something the file does not say.
    /// </summary>
    [Theory]
    [InlineData("MaintainListedPrices")]
    [InlineData("AutoCounterOffer")]
    public void Pressing_either_answer_writes_the_entry_and_the_two_write_opposite_values(string key)
    {
        AutomationForm form = Form(new AdvisorSettings());

        SettingChange no = Assert.Single(form.Press(TwoAnswers.No(key)));
        SettingChange yes = Assert.Single(form.Press(TwoAnswers.Yes(key)));

        Assert.Equal(key, no.Key);
        Assert.Equal(key, yes.Key);
        Assert.Equal("Off", no.Value);
        Assert.Equal("On", yes.Value);
    }

    /// <summary>
    /// The log line after a write is a reading of the page rather than of what
    /// was asked for, and a question is two rows keyed on neither the entry. The
    /// answer that is ticked is what the entry says.
    /// </summary>
    [Fact]
    public void A_question_reports_what_its_ticked_answer_says()
    {
        Assert.Equal("Off", Form(new AdvisorSettings()).ValueOf("AutoHandover"));
        Assert.Equal("On", Form(AllOn()).ValueOf("AutoHandover"));
    }

    // --- where a handover may happen -----------------------------------------

    /// <summary>
    /// Tickets 49 and 52: three answers, and <i>when I talk to them</i> is the
    /// one a fresh install shows. The owner's complaint was that the deal
    /// completed the moment its time arrived, wherever he was — <em>"Eigentlich
    /// möchte ich schon noch hinfahren. Und das sollte auch default sein."</em>
    /// Ticket 52 kept that answer and replaced its condition: <em>"Wenn ich
    /// hingehe, dann soll halt warten, bis ich E drücke und dann den Deal
    /// machen."</em>
    /// </summary>
    [Fact]
    public void The_handover_has_three_answers_and_talking_to_them_is_the_default()
    {
        IReadOnlyList<FormRow> rows = Block(Form(new AdvisorSettings()), "HANDOVER").Rows;

        Assert.Equal(
            new[]
            {
                TwoAnswers.No("AutoHandover"),
                TwoAnswers.Yes("AutoHandover"),
                TwoAnswers.Yes("HandoverFromAnywhere"),
                "HandoverFromAnywhere:multiplayer",
            },
            rows.Select(row => row.Id));

        Assert.Equal("Manual (game's default)", rows[0].Text);
        Assert.Equal("Automated handover when I talk to them", rows[1].Text);
        Assert.Equal("Automated handover from anywhere", rows[2].Text);

        Assert.Equal(
            new[] { FormRowKind.Choice, FormRowKind.Choice, FormRowKind.Choice, FormRowKind.Notice },
            rows.Select(row => row.Kind));
    }

    /// <summary>
    /// Exactly one of the three is ticked, whichever pair the file holds. This
    /// is the whole point of a question rather than two tick boxes: a player can
    /// read what the mod is doing off the page.
    /// </summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 2)]
    public void Exactly_one_answer_is_ticked(bool automated, bool anywhere, int ticked)
    {
        IReadOnlyList<FormRow> rows = Block(
            Form(new AdvisorSettings { AutoHandover = automated, HandoverFromAnywhere = anywhere }),
            "HANDOVER").Rows;

        Assert.Equal(
            new[] { ticked == 0, ticked == 1, ticked == 2 },
            rows.Take(3).Select(row => row.Chosen));
    }

    /// <summary>
    /// A file hand-edited into the pair the page cannot write — manual, and
    /// <i>from anywhere</i> on — draws as manual, which is what the mod then
    /// does: the gate stands down on the switch before it asks where.
    /// </summary>
    [Fact]
    public void Manual_beside_from_anywhere_draws_as_manual()
    {
        IReadOnlyList<FormRow> rows = Block(
            Form(new AdvisorSettings { AutoHandover = false, HandoverFromAnywhere = true }),
            "HANDOVER").Rows;

        Assert.True(rows[0].Chosen);
        Assert.False(rows[2].Chosen);
    }

    /// <summary>
    /// Every answer writes both entries, so the file can hold three states and
    /// no fourth. Writing only the one that changed would leave the pair above
    /// in the file for good.
    /// </summary>
    [Theory]
    [InlineData("AutoHandover:manual", "Off", "Off")]
    [InlineData("AutoHandover:automated", "On", "Off")]
    [InlineData("HandoverFromAnywhere:automated", "On", "On")]
    public void Every_answer_writes_both_entries(string row, string handover, string anywhere)
    {
        IReadOnlyList<SettingChange> written = Form(AllOn()).Press(row);

        Assert.Equal(
            new[] { "AutoHandover", "HandoverFromAnywhere" },
            written.Select(change => change.Key));
        Assert.Equal(new[] { handover, anywhere }, written.Select(change => change.Value));
    }

    /// <summary>
    /// And what the log says after one of those writes is read off the ticked
    /// answer, for both entries.
    /// </summary>
    [Fact]
    public void The_question_reports_both_entries_it_answers_for()
    {
        AutomationForm anywhere = Form(AllOn());
        AutomationForm atTheCustomer = Form(new AdvisorSettings { AutoHandover = true });

        Assert.Equal("On", anywhere.ValueOf("HandoverFromAnywhere"));
        Assert.Equal("Off", atTheCustomer.ValueOf("HandoverFromAnywhere"));
        Assert.Equal("On", atTheCustomer.ValueOf("AutoHandover"));
    }

    /// <summary>
    /// The owner asked for the line, and for it to say where the goods come
    /// from: <em>"From anywhere sollte auch einen Hinweis haben, dass jeder
    /// weiß: okay, wenn ich im Netzwerk unterwegs bin, dann geht das immer vom
    /// Host."</em> It is one row rather than a line under the answer, because a
    /// player has to be able to read it <em>before</em> choosing.
    /// <para>
    /// And it says the second half too, which he decided after asking whether
    /// his <i>from anywhere</i> overrode a guest who had chosen otherwise. It
    /// does: the contract list is shared and his copy reaches it from across the
    /// map, so the guest's own answer never gets a turn. He was offered hiding
    /// the option in multiplayer, keeping it with the warning, or gating it on
    /// contracts about to expire, and he kept it with the warning. A later
    /// reader who softens this line is undoing a decision.
    /// </para>
    /// </summary>
    [Fact]
    public void From_anywhere_says_whose_goods_go_and_that_it_overrides_the_others()
    {
        FormRow note = Row(Form(new AdvisorSettings()), "HandoverFromAnywhere:multiplayer");

        Assert.Equal(FormRowKind.Notice, note.Kind);
        Assert.Contains("multiplayer", note.Text);
        Assert.Contains("host", note.Text);
        Assert.Contains("whatever others chose", note.Text);
        Assert.Empty(note.Press);

        // Drawn whichever answer is ticked, because it is what choosing the
        // third one costs rather than a report on having chosen it.
        Assert.Contains(
            Block(Form(AllOn()), "HANDOVER").Rows,
            row => row.Id == "HandoverFromAnywhere:multiplayer");
    }

    // --- the chance floor ----------------------------------------------------

    /// <summary>
    /// A floor on offers that are never made is not a setting that currently
    /// applies, and the page does not draw those: the dependency is drawn rather
    /// than described.
    /// </summary>
    [Fact]
    public void The_chance_is_not_on_the_page_while_negotiation_is_manual()
    {
        AutomationBlock block = Block(Form(new AdvisorSettings()), "PRICE NEGOTIATION");

        Assert.Equal(2, block.Rows.Count);
        Assert.DoesNotContain(block.Rows, row => row.Id == "AcceptanceProbabilityThreshold");

        // And it is still the block's to answer for, switched on or not.
        Assert.Contains("AcceptanceProbabilityThreshold", block.Claims);
    }

    /// <summary>
    /// Stories 14, 18 and 19 as they ended up: one number for the mod's caution,
    /// under the answer that switches the negotiation on and one level further
    /// in. It is <c>Never offer below this chance</c> and not <c>aim for</c> —
    /// the search picks the rung that pays best and this only refuses what is
    /// too risky for this player.
    /// </summary>
    [Fact]
    public void The_chance_sits_under_the_automated_answer_as_one_number()
    {
        FormRow chance = Block(Form(AllOn()), "PRICE NEGOTIATION").Rows.Last();

        Assert.Equal("AcceptanceProbabilityThreshold", chance.Id);
        Assert.Equal(FormRowKind.Stepper, chance.Kind);
        Assert.Equal("Never offer below this chance", chance.Text);
        Assert.Equal("90%", chance.Value);
        Assert.Equal(2, chance.Indent);

        // Host-only is asked of the block, which is what decides whether a guest
        // is drawn it. The row used to carry a copy of the flag that nothing but
        // a test ever read.
        Assert.True(Block(Form(AllOn()), "PRICE NEGOTIATION").HostOnly);
    }

    /// <summary>
    /// The two buttons of the stepper are blunt at the ends, and the row's own
    /// single button — the fallback where a build lends no stepper — is not: it
    /// comes round to 50%, the way every other row's press does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Found by the independent review, finding 16. The fallback used to carry
    /// the range's own <c>Next</c>, which is empty at the top, so on such a
    /// build the control stepped up only and then jammed: at 100% the floor
    /// could be moved no further except by editing <c>MelonPreferences.cfg</c>.
    /// The comment beside it said the fallback "steps the way the rest of the
    /// app's rows do" — and the rest use <c>AutomationChoice.Ladder</c>, which
    /// wraps, while this is a <c>Range</c>, which is blunt by design. Two
    /// sentences about two different types.
    /// </para>
    /// <para>
    /// A blunt end on a control with a <c>−</c> beside the <c>+</c> is what
    /// <c>app.md</c> asks for and is kept. A single button that cannot come back
    /// is a different thing.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_chances_single_button_fallback_comes_round_rather_than_jamming()
    {
        FormRow top = Chance(new AdvisorSettings { AcceptanceProbabilityThreshold = 1f });

        // The stepper's own two: blunt at the top, and a way down.
        Assert.Empty(top.Up);
        Assert.Equal("0.95", Assert.Single(top.Down).Value);

        // The row's own button, which is all a build without a stepper has.
        Assert.Equal("0.5", Assert.Single(top.Press).Value);

        // And below the top it is simply the next rung up, as before.
        Assert.Equal("0.95", Assert.Single(Chance(new AdvisorSettings()).Press).Value);
    }

    /// <summary>The chance row, with the negotiation switched on so it is drawn.</summary>
    private static FormRow Chance(AdvisorSettings settings)
    {
        settings.AutoCounterOffer = true;

        return Block(Form(settings), "PRICE NEGOTIATION").Rows.Last();
    }

    /// <summary>
    /// <b>And it carries no footnote.</b> The line the drawing put here —
    /// "counts the relationship; the game's figure does not" — was read out of
    /// the binary by ticket 37 and is false on both halves: the probe already
    /// counts the relationship through the customer's budget and order days, and
    /// what it cannot count is a leniency term no function of the probe's output
    /// recovers. The other sentence, that a refusal costs relationship, is false
    /// too: the -0.5 belongs to <c>CustomerRejectedDeal</c> and the counteroffer
    /// path never reaches it. See the project notes.
    /// </summary>
    [Fact]
    public void The_chance_carries_no_footnote()
    {
        FormRow chance = Row(Form(AllOn()), "AcceptanceProbabilityThreshold");

        Assert.Equal(string.Empty, chance.Hint);
    }

    /// <summary>
    /// The file holds the chance itself and the row shows whole percent, which
    /// is the one row on the page that is not the file's own text. The game
    /// shows the player a percentage and the drawing asks for one.
    /// </summary>
    [Fact]
    public void The_chance_is_drawn_as_whole_percent_and_written_as_a_fraction()
    {
        AutomationForm form = Form(AllOn(0.75f));

        Assert.Equal("75%", form.ValueOf("AcceptanceProbabilityThreshold"));
        Assert.Equal("0.8", Assert.Single(form.Step("AcceptanceProbabilityThreshold", up: true)).Value);
    }

    [Fact]
    public void The_two_buttons_step_the_chance_in_opposite_directions()
    {
        AutomationForm form = Form(AllOn());

        Assert.Equal("0.95", Stepped(form, up: true));
        Assert.Equal("0.85", Stepped(form, up: false));
    }

    /// <summary>
    /// Both ends are blunt: the button that would leave the range writes
    /// nothing, so nothing is written, so the page is not redrawn and the number
    /// does not move. A wrap would take one press at 100% to 50%, which is the
    /// opposite of what a player standing at either end asked for.
    /// </summary>
    [Fact]
    public void The_ends_of_the_chance_are_blunt_and_both_are_reachable()
    {
        Assert.Empty(Form(AllOn(1f)).Step("AcceptanceProbabilityThreshold", up: true));
        Assert.Equal("0.95", Stepped(Form(AllOn(1f)), up: false));

        Assert.Empty(Form(AllOn(0.5f)).Step("AcceptanceProbabilityThreshold", up: false));
        Assert.Equal("0.55", Stepped(Form(AllOn(0.5f)), up: true));

        Assert.Equal("100%", Form(AllOn(1f)).ValueOf("AcceptanceProbabilityThreshold"));
        Assert.Equal("50%", Form(AllOn(0.5f)).ValueOf("AcceptanceProbabilityThreshold"));
    }

    /// <summary>
    /// The number is typed as well as stepped. The owner asked for it twice and
    /// in capitals, and the field is the game's own input — it was switched off,
    /// not missing.
    /// </summary>
    [Theory]
    [InlineData("75", "0.75")]
    [InlineData("50", "0.5")]
    [InlineData("100", "1")]
    [InlineData(" 82 ", "0.82")]
    [InlineData("82%", "0.82")]
    public void A_typed_chance_inside_the_range_is_written_as_the_file_spells_it(
        string typed, string written)
    {
        SettingChange change = Assert.Single(
            Form(AllOn()).Type("AcceptanceProbabilityThreshold", typed));

        Assert.Equal("AcceptanceProbabilityThreshold", change.Key);
        Assert.Equal(written, change.Value);
    }

    /// <summary>
    /// <b>Out of range reverts, and it never clamps.</b> A typed 30 writes
    /// nothing, so the field goes back to what the file still says. Clamping it
    /// to 50 would turn a typo into a setting and never tell the player that 30
    /// was refused.
    /// </summary>
    [Theory]
    [InlineData("30")]
    [InlineData("49.9")]
    [InlineData("101")]
    [InlineData("0.9")]
    [InlineData("soon")]
    [InlineData("")]
    [InlineData("  ")]
    public void A_typed_chance_outside_the_range_writes_nothing_at_all(string typed)
    {
        Assert.Empty(Form(AllOn()).Type("AcceptanceProbabilityThreshold", typed));
    }

    /// <summary>
    /// Nothing else on the page takes typing, so a stray commit into some other
    /// row cannot write a setting.
    /// </summary>
    [Fact]
    public void No_other_row_takes_anything_typed_into_it()
    {
        AutomationForm form = Form(AllOn(), Everything(), Hours(), maintainPrices: true);

        foreach (FormRow row in Rows(form).Where(row => row.Id != "AcceptanceProbabilityThreshold"))
        {
            Assert.Empty(form.Type(row.Id, "75"));
        }
    }

    [Fact]
    public void Stepping_or_typing_a_row_that_is_not_on_screen_writes_nothing()
    {
        Assert.Empty(Form(new AdvisorSettings()).Step("AcceptanceProbabilityThreshold", up: true));
        Assert.Empty(Form(new AdvisorSettings()).Type("AcceptanceProbabilityThreshold", "75"));
    }

    // --- the accepted schedule -----------------------------------------------

    /// <summary>
    /// Story 20: four windows and no switch above them, because four ticked
    /// boxes already say which windows are allowed. Their hours are the game's,
    /// read at runtime and never written down here.
    /// </summary>
    [Fact]
    public void The_schedule_is_four_windows_with_the_hours_the_game_reports_and_no_switch()
    {
        var allowed = new DealWindowSet();
        allowed.Allow(DealWindow.Afternoon, true);

        AutomationBlock block = Block(
            Form(new AdvisorSettings(), allowed, Hours()), "ACCEPTED SCHEDULE");

        Assert.All(block.Rows, row => Assert.Equal(FormRowKind.Toggle, row.Kind));
        Assert.Equal(
            new[] { "Morning", "Afternoon", "Night", "Late Night" },
            block.Rows.Select(row => row.Text));
        Assert.Equal(
            new[] { "06:00 to 12:00", "12:00 to 18:00", "18:00 to 24:00", "00:00 to 06:00" },
            block.Rows.Select(row => row.Value));
        Assert.Equal(new[] { false, true, false, false }, block.Rows.Select(row => row.Chosen));
    }

    /// <summary>
    /// Story 3 for the schedule, and the one that was not true before this
    /// ticket: a fresh install schedules nothing.
    /// </summary>
    [Fact]
    public void A_fresh_install_allows_no_window_at_all()
    {
        AutomationBlock block = Block(Form(new AdvisorSettings(), hours: Hours()), "ACCEPTED SCHEDULE");

        Assert.Equal(4, block.Rows.Count);
        Assert.All(block.Rows, row => Assert.False(row.Chosen, row.Id));
    }

    [Fact]
    public void Pressing_a_window_asks_for_the_other_state()
    {
        SettingChange change = Assert.Single(
            Form(new AdvisorSettings(), new DealWindowSet(), Hours()).Press("AllowMorningWindow"));

        Assert.Equal("On", change.Value);
    }

    // --- the handover block --------------------------------------------------

    /// <summary>
    /// Ticket 34's guard, narrowed. It used to assert that the handover block is
    /// one row, and that is not what it was protecting: the owner deleted the
    /// board of one row per live contract, because the game already lists his
    /// contracts and their times down the left of the screen and nobody opens a
    /// phone app to read that twice. <em>"Ich werde doch nie da reingucken."</em>
    /// </summary>
    /// <remarks>
    /// So what is asserted is the absence of contract rows rather than a count —
    /// which is what the guard's second assertion already checked, and what
    /// leaves the block free to grow the answers the drawing gives it. There is
    /// no longer a kind of row that means one contract, so the check is that
    /// nothing under the handover names one.
    /// </remarks>
    [Fact]
    public void The_handover_block_carries_no_contracts()
    {
        AutomationBlock handover = Block(Form(AllOn()), "HANDOVER");

        Assert.DoesNotContain(
            handover.Rows,
            row => row.Id.Contains("contract", StringComparison.OrdinalIgnoreCase));

        // Nothing under it names a customer or a time either, which is what the
        // deleted rows carried.
        Assert.All(
            handover.Rows,
            row => Assert.True(
                row.Kind is FormRowKind.Choice or FormRowKind.Notice,
                $"{row.Id} is a {row.Kind}, which is not a thing this block asks"));
    }

    /// <summary>
    /// Story 22: the choice between delivering exactly what was ordered and
    /// letting the automation round the grade up, drawn as the drawing draws it —
    /// inside the switch it depends on, with the footnote under both answers.
    /// </summary>
    [Fact]
    public void The_handover_asks_which_grade_may_go_out_and_says_what_it_cannot_promise()
    {
        IReadOnlyList<FormRow> rows = Block(Form(AllOn()), "HANDOVER").Rows;

        Assert.Equal(
            new[]
            {
                TwoAnswers.No("AutoHandover"),
                TwoAnswers.Yes("AutoHandover"),
                TwoAnswers.Yes("HandoverFromAnywhere"),
                "HandoverFromAnywhere:multiplayer",
                TwoAnswers.No("MayUseAHigherGrade"),
                TwoAnswers.Yes("MayUseAHigherGrade"),
                "MayUseAHigherGrade:packages",
            },
            rows.Select(row => row.Id));

        Assert.Equal("Exactly the grade that was ordered", rows[4].Text);
        Assert.Equal("May use a higher grade", rows[5].Text);

        // A step further in than the switch, because it only applies while the
        // switch is on.
        Assert.Equal(new[] { 2, 2, 2 }, rows.Skip(4).Select(row => row.Indent));

        // The one thing the control cannot promise, said once rather than per
        // delivery: packages are indivisible, so exactness is about the grade.
        Assert.Equal(FormRowKind.Notice, rows[6].Kind);
        Assert.Contains("packages cannot be split", rows[6].Text);
        Assert.Empty(rows[6].Press);
    }

    /// <summary>
    /// <i>May use a higher grade</i> is what a fresh install does, and it is the
    /// answer that is ticked rather than the absence of one. Both circles are
    /// drawn either way; which one is filled is the whole of what this checks,
    /// and <c>app.md</c> fills the second.
    /// </summary>
    [Fact]
    public void May_use_a_higher_grade_is_the_answer_a_fresh_install_shows()
    {
        IReadOnlyList<FormRow> rows = Block(
            Form(new AdvisorSettings { AutoHandover = true }), "HANDOVER").Rows;

        Assert.False(rows[4].Chosen);
        Assert.True(rows[5].Chosen);

        // And what the log says after a write is read off the ticked answer, as
        // it is for every other question on the page.
        Assert.Equal(
            "On",
            Form(new AdvisorSettings { AutoHandover = true }).ValueOf("MayUseAHigherGrade"));
        Assert.Equal(
            "Off",
            Form(new AdvisorSettings { AutoHandover = true, MayUseAHigherGrade = false })
                .ValueOf("MayUseAHigherGrade"));
    }

    /// <summary>
    /// And it is not drawn at all while the handover is on the game's own
    /// behaviour, because there is then nothing for it to constrain. The setting
    /// is still the block's to answer for, so the key stays claimed.
    /// </summary>
    [Fact]
    public void The_grade_question_is_not_drawn_while_the_handover_is_manual()
    {
        AutomationBlock handover = Block(Form(new AdvisorSettings()), "HANDOVER");

        // The three answers and the line under the third, and nothing else.
        Assert.Equal(4, handover.Rows.Count);
        Assert.DoesNotContain(
            handover.Rows,
            row => row.Id.StartsWith("MayUseAHigherGrade", StringComparison.Ordinal));
        Assert.Contains("MayUseAHigherGrade", handover.Claims);
    }

    /// <summary>
    /// Either answer writes the entry, as every other question on the page does.
    /// </summary>
    [Fact]
    public void Pressing_either_grade_answer_writes_the_entry()
    {
        AutomationForm form = Form(AllOn());

        SettingChange exactly = Assert.Single(form.Press(TwoAnswers.No("MayUseAHigherGrade")));
        SettingChange higher = Assert.Single(form.Press(TwoAnswers.Yes("MayUseAHigherGrade")));

        Assert.Equal("MayUseAHigherGrade", exactly.Key);
        Assert.Equal("Off", exactly.Value);
        Assert.Equal("MayUseAHigherGrade", higher.Key);
        Assert.Equal("On", higher.Value);
    }

    /// <summary>
    /// And the key those rows answered to writes nothing, because there is no
    /// such row on screen.
    /// </summary>
    [Fact]
    public void Pressing_the_key_a_contract_row_used_to_answer_to_writes_nothing()
    {
        Assert.Empty(Form(AllOn()).Press("AutoHandover:contract:0"));
    }

    [Fact]
    public void Pressing_a_row_that_is_not_on_screen_writes_nothing()
    {
        Assert.Empty(Form(new AdvisorSettings()).Press("SettingThatWent"));
    }

    // --- prose ---------------------------------------------------------------

    /// <summary>
    /// There is nowhere left in the model to put a paragraph, and the one line
    /// that is left is held to a length. Where a control needs a paragraph, the
    /// control is wrong.
    /// </summary>
    [Fact]
    public void No_row_of_the_page_carries_more_than_one_short_line()
    {
        foreach (FormRow row in Rows(Form(AllOn(), maintainPrices: true)))
        {
            Assert.True(
                row.Hint.Length <= AutomationSetting.SummaryLimit,
                $"{row.Id} says {row.Hint.Length} characters: \"{row.Hint}\"");
        }
    }

    /// <summary>
    /// The list column measures 63 characters at the app's own font size, which
    /// is what every row on this page has to live inside.
    /// </summary>
    [Fact]
    public void No_row_is_wider_than_the_column_the_game_reports()
    {
        const int column = 63;

        foreach (FormRow row in Rows(Form(AllOn(), Everything(), Hours(), maintainPrices: true)))
        {
            Assert.True(
                row.Text.Length + row.Value.Length <= column,
                $"{row.Id} is {row.Text.Length + row.Value.Length} characters wide");
        }
    }

    /// <summary>
    /// Stories 9 and 10, held where they can fail: no status word and no count
    /// reaches a row. The gates still say those things; they go to the debug
    /// file.
    /// </summary>
    [Fact]
    public void No_row_says_running_or_waiting_or_counts_anything()
    {
        string[] banned = { "running", "waiting", "answered", "maintained", "last:" };

        foreach (FormRow row in Rows(Form(AllOn(), Everything(), Hours(), maintainPrices: true)))
        {
            string said = $"{row.Text} {row.Value} {row.Hint}";

            Assert.DoesNotContain(
                banned, word => said.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    // --- helpers -------------------------------------------------------------

    private static AdvisorSettings AllOn() => AllOn(new AdvisorSettings().AcceptanceProbabilityThreshold);

    private static AdvisorSettings AllOn(float chance) => new()
    {
        AutoCounterOffer = true,
        AutoHandover = true,
        HandoverFromAnywhere = true,
        MayUseAHigherGrade = true,
        AcceptanceProbabilityThreshold = chance,
    };

    /// <summary>What one press of the chance's own <c>+</c> or <c>−</c> writes.</summary>
    private static string Stepped(AutomationForm form, bool up) =>
        Assert.Single(form.Step("AcceptanceProbabilityThreshold", up)).Value;

    private static DealWindowSet Everything()
    {
        var windows = new DealWindowSet();
        foreach (DealWindow window in DealWindowSet.All)
        {
            windows.Allow(window, true);
        }

        return windows;
    }

    private static IReadOnlyList<DealWindowHours> Hours() => new[]
    {
        new DealWindowHours(DealWindow.Morning, 600, 1200),
        new DealWindowHours(DealWindow.Afternoon, 1200, 1800),
        new DealWindowHours(DealWindow.Night, 1800, 2400),
        new DealWindowHours(DealWindow.LateNight, 0, 600),
    };

    private static IReadOnlyList<AutomationSetting> AllSettings(
        AdvisorSettings settings,
        DealWindowSet? allowed = null,
        IReadOnlyList<DealWindowHours>? hours = null,
        bool maintainPrices = false) =>
        AutomationCatalog.Describe(settings)
            .Concat(DealWindowCatalog.Describe(
                allowed ?? new DealWindowSet(),
                hours ?? Array.Empty<DealWindowHours>()))
            .Concat(PriceMaintenanceCatalog.Describe(maintainPrices))
            .ToArray();

    /// <summary>
    /// The page as the host sees it, which is what all but the host-and-guest
    /// tests are about. A single player is the host, so this is also solo play.
    /// </summary>
    private static AutomationForm Form(
        AdvisorSettings settings,
        DealWindowSet? allowed = null,
        IReadOnlyList<DealWindowHours>? hours = null,
        bool maintainPrices = false) =>
        AutomationForm.Build(
            AllSettings(settings, allowed, hours, maintainPrices), ServerAuthority.Held);

    private static AutomationBlock Block(AutomationForm form, string title) =>
        form.Blocks.Single(block => block.Title == title);

    private static IEnumerable<FormRow> Rows(AutomationForm form) =>
        form.Blocks.SelectMany(block => block.Rows);

    private static FormRow Row(AutomationForm form, string id) =>
        Rows(form).Single(row => row.Id == id);
}
