using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// One feature of the mod: its switch, and everything that only matters while
/// that switch is on.
/// </summary>
public sealed class AutomationBlock
{
    public AutomationBlock(
        string title,
        IReadOnlyList<FormRow> rows,
        IReadOnlyList<string> claims,
        bool hostOnly = false)
    {
        Title = title;
        Rows = rows;
        Claims = claims;
        HostOnly = hostOnly;
    }

    /// <summary>The heading, in the game's own section type. May be empty.</summary>
    public string Title { get; }

    /// <summary>
    /// What is on screen, in reading order. A block whose switch is off holds
    /// only that switch: a setting that does not currently apply is not drawn,
    /// which is how the dependency becomes visible.
    /// </summary>
    public IReadOnlyList<FormRow> Rows { get; }

    /// <summary>
    /// Every MelonPreferences entry this block is responsible for, whether or
    /// not its switch is currently showing it. What makes "every setting is
    /// reachable from the app" checkable.
    /// </summary>
    public IReadOnlyList<string> Claims { get; }

    /// <summary>
    /// Whether every setting this block answers for only does anything on the
    /// host, and the block is therefore not built on a guest at all.
    /// </summary>
    /// <remarks>
    /// Read off <see cref="AutomationSetting.HostOnly"/>, which has been on every
    /// setting since they were written and which no line of the interface ever
    /// drew. Three of the four blocks act on state the session shares — one
    /// customer roster, one contract list, one set of listed prices — and a
    /// guest's copy of those switches changed nothing on his machine while they
    /// were on his page.
    /// </remarks>
    public bool HostOnly { get; }
}

/// <summary>
/// The app's one page: a guided form rather than a flat list of switches, with
/// every block headed by the question it answers.
/// </summary>
/// <remarks>
/// <para>
/// The owner's verdict on the list it replaces was that it is "a spreadsheet
/// where I click things and have no idea what the side effects are", and the
/// diagnosis in it is exact: "many of these things have causal dependencies and
/// I cannot see any of them". Every setting sat at the same level, so nothing on
/// screen said which setting mattered only while another was on. Each feature is
/// now a block headed by its switch, and its dependent settings are not built at
/// all while that switch is off.
/// </para>
/// <para>
/// <b>Nothing on this page is a reading.</b> There used to be a second input —
/// a status word per block, a count per pass, the last handover's bonuses — and
/// the owner deleted every one of them: <em>"Ich gehe davon aus, wenn ich es
/// gesetzt habe, dann läuft es."</em> and <em>"Interessiert niemanden."</em> The
/// form is a function of the settings alone, so a later worker looking for where
/// the gates' sentences went will find that they go to the debug file and
/// nowhere else.
/// </para>
/// <para>
/// <b>Whose machine it is on is an input too.</b> Three of the four blocks act
/// on state the session shares — one customer roster, one contract list, one set
/// of listed prices — and they run on the host, so on a guest they are not built
/// and <see cref="HostOnlyNote"/> stands in their place. The handover is the
/// exception and always was. Nothing is sent either way: the host's settings are
/// not shown to a guest, because showing them means sending them.
/// </para>
/// <para>
/// Nothing here is a second store. The form is built from the same
/// <see cref="AutomationSetting"/> list the preferences file produces, it is
/// rebuilt from a fresh reading after every write, and every press it answers
/// with is a <see cref="SettingChange"/> for an entry that already owns the
/// value.
/// </para>
/// </remarks>
public sealed class AutomationForm
{
    /// <summary>
    /// Settings that live in the preferences file and are deliberately not on
    /// the page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>There is one: the handover ledger.</b> It is in
    /// <c>MelonPreferences.cfg</c>, it is off there, and the app does not know it
    /// exists — because the owner asked for the bonuses to be recorded so they
    /// could be read afterwards and explicitly not surfaced again. A row in the
    /// Automation section would make an instrument into a feature. This list is
    /// where that exemption is written down, in the product, because this
    /// property is what a reader of the product code consults when they ask
    /// whether the file holds anything the page does not.
    /// </para>
    /// <para>
    /// <b>It said "there are none" for as long as the ledger has shipped.</b>
    /// The exemption was real and was recorded — but only in the test assembly,
    /// so the one place whose documented job is to list such settings claimed
    /// its own emptiness as the spec's promise kept. Everything else here is
    /// still true: every setting the page stopped drawing was deleted from the
    /// file with it, and the acceptance threshold is on the page now as the
    /// chance floor under the negotiation block.
    /// </para>
    /// <para>
    /// <b>Why the ledger is not simply keyless, like the debug record.</b> The
    /// record costs nothing while nothing is happening and touches no game code.
    /// The ledger installs a Harmony patch on the game's own handover path, and
    /// installs it only once the setting has read as on, so a host who has not
    /// asked for it pays nothing — not a detour, not a branch. Making it always
    /// on would put that detour on every handover in the game for a file the
    /// owner did not ask to have written.
    /// </para>
    /// <para>
    /// A test holds this list plus every block's claims against every key the
    /// adapter declares, so a setting cannot go missing from the app by accident
    /// — only on purpose, here.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> FileOnly { get; } = new[] { "HandoverLedger" };

    private readonly List<AutomationBlock> _blocks;
    private readonly Dictionary<string, FormRow> _rows = new();

    /// <summary>
    /// What each MelonPreferences entry currently reads as, by entry name rather
    /// than by row. A two-answer question is two rows and neither is keyed on
    /// the entry, so without this the log after a write would say nothing at all
    /// about the setting that was written.
    /// </summary>
    private readonly Dictionary<string, string> _says = new();

    private readonly List<string> _claimed = new();

    /// <summary>What the title bar says on the machine the mod is running on.</summary>
    public const string HostMode = "HOST MODE";

    /// <summary>And what it says on a guest's.</summary>
    public const string ClientMode = "CLIENT MODE";

    private AutomationForm(List<AutomationBlock> blocks, string mode)
    {
        _blocks = blocks;
        Mode = mode;

        foreach (AutomationBlock block in blocks)
        {
            foreach (FormRow row in block.Rows)
            {
                _rows[row.Id] = row;
                Says(row);
            }

            _claimed.AddRange(block.Claims);
        }
    }

    /// <summary>
    /// What one row says about the entry behind it. A toggle says it by its own
    /// tick; a question says it through whichever of its two answers is ticked,
    /// because that answer holds the value pressing it would write.
    /// </summary>
    private void Says(FormRow row)
    {
        if (row.Kind == FormRowKind.Toggle)
        {
            _says[row.Id] = AutomationSetting.OnOff(row.Chosen);
            return;
        }

        if (row.Kind != FormRowKind.Choice || !row.Chosen)
        {
            return;
        }

        foreach (SettingChange change in row.Press)
        {
            _says[change.Key] = change.Value;
        }
    }

    public IReadOnlyList<AutomationBlock> Blocks => _blocks;

    /// <summary>
    /// The word in the title bar, beside the app's name:
    /// <see cref="HostMode"/> or <see cref="ClientMode"/>.
    /// </summary>
    /// <remarks>
    /// It is there so a guest can tell at a glance why his page is shorter than
    /// the host's, rather than reading a short page as a broken one. A single
    /// player draws <see cref="HostMode"/>, because a single player is the
    /// server.
    /// </remarks>
    public string Mode { get; }

    /// <summary>
    /// Every MelonPreferences entry some block is responsible for. A setting
    /// missing from this and from <see cref="FileOnly"/> cannot be reached from
    /// the app at all; a test holds both against the settings list.
    /// </summary>
    /// <remarks>
    /// That test is about the host's page, which is where every setting is
    /// reachable. A guest's page does not build the host-only blocks at all, so
    /// it claims the handover's keys and nothing else — which is the point of it
    /// rather than a setting gone missing.
    /// </remarks>
    public IReadOnlyList<string> Claimed => _claimed;

    /// <summary>
    /// A player pressed a row. Answers what to write, in order, or nothing where
    /// a press does nothing — a notice, or a setting the phone has nowhere to
    /// type into.
    /// </summary>
    public IReadOnlyList<SettingChange> Press(string rowId) =>
        _rows.TryGetValue(rowId, out FormRow? row) ? row.Press : Array.Empty<SettingChange>();

    /// <summary>
    /// A player pressed one end of a stepper. Answers what to write, or nothing
    /// at the end the control does not go past — which is how a blunt end says
    /// so: the write never happens, so the page is never redrawn, so the number
    /// does not move.
    /// </summary>
    public IReadOnlyList<SettingChange> Step(string rowId, bool up)
    {
        if (!_rows.TryGetValue(rowId, out FormRow? row))
        {
            return Array.Empty<SettingChange>();
        }

        return up ? row.Up : row.Down;
    }

    /// <summary>
    /// A player typed into a row's field and committed it. Answers what to
    /// write, or nothing where the entry is refused.
    /// </summary>
    /// <remarks>
    /// <b>Nothing means revert, and never clamp.</b> An entry outside the
    /// control's range, or one that is not a number at all, writes nothing and
    /// the field goes back to the value the file still holds. Clamping to the
    /// nearest end would turn a typo into a setting without ever telling the
    /// player that what they typed was refused.
    /// </remarks>
    public IReadOnlyList<SettingChange> Type(string rowId, string? text)
    {
        if (!_rows.TryGetValue(rowId, out FormRow? row))
        {
            return Array.Empty<SettingChange>();
        }

        SettingChange? change = row.Typed.Write(text);

        return change is null ? Array.Empty<SettingChange>() : new[] { change };
    }

    /// <summary>
    /// What a control currently shows, or an empty string for one that is not on
    /// screen. Said in the log after a write, so what goes into the log is a
    /// reading of the file rather than what was asked for.
    /// </summary>
    public string ValueOf(string key)
    {
        if (_says.TryGetValue(key, out string? said))
        {
            return said;
        }

        return _rows.TryGetValue(key, out FormRow? row) ? row.Value : string.Empty;
    }

    /// <summary>
    /// Build the page from what the preferences file currently says.
    /// </summary>
    /// <param name="settings">
    /// Every setting the mod holds, from every catalogue. Nothing is invented
    /// here and nothing is silently left out: a setting neither claimed nor
    /// named file-only fails the test that says so.
    /// </param>
    /// <param name="authority">
    /// Whose machine this is, from the one reader every pass already asks before
    /// it acts. The page gives the same answer they do rather than working one
    /// out of its own.
    /// </param>
    public static AutomationForm Build(
        IReadOnlyList<AutomationSetting> settings, ServerAuthority authority)
    {
        var index = new Dictionary<string, AutomationSetting>();
        foreach (AutomationSetting setting in settings)
        {
            index[setting.Key] = setting;
        }

        var shop = new Catalogue(index);

        var blocks = new List<AutomationBlock>
        {
            ListedPrice(shop),
            PriceNegotiation(shop),
            AcceptedSchedule(shop),
            Handover(shop),
        };

        return TheHost(authority)
            ? new AutomationForm(blocks, HostMode)
            : new AutomationForm(AGuestsPage(blocks), ClientMode);
    }

    /// <summary>
    /// Whether this machine is the one the host-only settings belong to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single player is one of them: <see cref="ServerAuthority.Held"/> is
    /// server and client both up, which is what one player alone is, so solo play
    /// draws the whole page and <see cref="HostMode"/> above it.
    /// <see cref="ServerAuthority.WithoutAClient"/> is the server's own machine
    /// with its client half not yet up, which is still the machine these settings
    /// act on.
    /// </para>
    /// <para>
    /// <b>Everything else draws a guest's page, including a reading that could
    /// not be taken.</b> That is the same safe end the passes take — a machine
    /// that cannot say it is the server does not act as one — and here it costs
    /// nothing that lasts: the page is rebuilt from a fresh reading every time
    /// the app is opened and after every write, so a reading taken before the
    /// session was up is replaced by the next one rather than kept.
    /// </para>
    /// </remarks>
    private static bool TheHost(ServerAuthority authority) =>
        authority is ServerAuthority.Held or ServerAuthority.WithoutAClient;

    /// <summary>
    /// The page a guest gets: the note, and the blocks that are his to set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="AutomationBlock.HostOnly"/> is what decides, so the rule is the
    /// settings' own flag rather than a second list of block names that could
    /// drift from it. Three of the four say yes today; the handover says no, and
    /// always did, because it is one player's goods leaving one player's pockets.
    /// </para>
    /// <para>
    /// A block this build carries no settings for holds no rows, and is left out
    /// here as well: a guest reading the note and then finding an empty heading
    /// under it would be reading the page for a feature that is not in the build.
    /// </para>
    /// </remarks>
    private static List<AutomationBlock> AGuestsPage(List<AutomationBlock> blocks)
    {
        var his = new List<AutomationBlock>(blocks.Count)
        {
            new(string.Empty, HostOnlyNote.Rows(), Array.Empty<string>()),
        };

        foreach (AutomationBlock block in blocks)
        {
            if (!block.HostOnly && block.Rows.Count > 0)
            {
                his.Add(block);
            }
        }

        return his;
    }

    /// <summary>
    /// The one question about the player's own listed prices, with its two
    /// answers.
    /// </summary>
    /// <remarks>
    /// Nothing is added to Schedule I's own Products app and nothing here
    /// pretends to be part of it. The price lives in <c>ProductManager</c>; this
    /// block decides whether Dealcraft writes it.
    /// </remarks>
    private static AutomationBlock ListedPrice(Catalogue shop)
    {
        var claims = new[] { PriceMaintenanceCatalog.Key };

        AutomationSetting? setting = shop.Find(PriceMaintenanceCatalog.Key);

        // A feature left out of this build has nothing to answer.
        return new AutomationBlock(
            "LISTED PRICE",
            setting is null ? Array.Empty<FormRow>() : PriceMaintenanceCatalog.Rows(setting),
            claims,
            shop.HostOnly(claims));
    }

    /// <summary>
    /// The one question about answering a customer's request, with its two
    /// answers. Named for what it does rather than for the mechanism: the owner
    /// asked for <c>PRICE NEGOTIATION</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One thing under it, and only while the automation is on: the chance
    /// floor. The <em>price</em> floor a counter holds to is the player's own
    /// listed price, which is not a setting and so is not a row.
    /// </para>
    /// <para>
    /// The chance is a floor and not a target — <see cref="ChanceFloor"/> — and
    /// it carries no footnote. The line the drawing once put here, <em>"counts
    /// the relationship; the game's figure does not"</em>, was read out of the
    /// binary by ticket 37 and is false on both halves.
    /// </para>
    /// </remarks>
    private static AutomationBlock PriceNegotiation(Catalogue shop)
    {
        var claims = new[] { "AutoCounterOffer", ChanceFloor.Key };
        var rows = new List<FormRow>(shop.Question("AutoCounterOffer"));

        // Inside the answer that switches it on, one level further in: a floor
        // on offers that are never made is not a setting that currently
        // applies, and the page does not draw those.
        if (shop.IsOn("AutoCounterOffer"))
        {
            shop.Chance(rows, indent: 2);
        }

        return new AutomationBlock("PRICE NEGOTIATION", rows, claims, shop.HostOnly(claims));
    }

    /// <summary>
    /// The scheduling block: four windows with the clock times the game reports
    /// for them, and no switch above them.
    /// </summary>
    /// <remarks>
    /// <c>AutoScheduleDeals</c> was that switch and it is deleted: four ticked
    /// boxes already say which windows are allowed, and a parent switch above
    /// them can only contradict them. <em>"Vier angehakte Fenster sagen doch
    /// schon, welche Fenster ich erlaube."</em>
    ///
    /// None of the four is ticked on a fresh install, and that had to be fixed
    /// in the code rather than drawn: Late Night used to ship on, harmless only
    /// while the parent switch was off above it.
    /// </remarks>
    private static AutomationBlock AcceptedSchedule(Catalogue shop)
    {
        var rows = new List<FormRow>();
        var claims = new List<string>();

        foreach (DealWindow window in DealWindowSet.All)
        {
            claims.Add(DealWindowCatalog.KeyOf(window));
            shop.Window(rows, DealWindowCatalog.KeyOf(window));
        }

        return new AutomationBlock("ACCEPTED SCHEDULE", rows, claims, shop.HostOnly(claims));
    }

    /// <summary>
    /// The handover block: one question with three answers, and the one question
    /// under it that only matters while the handover is automated — how far above
    /// the grade a contract asked for a delivery may go.
    /// </summary>
    /// <remarks>
    /// <b>Three answers, and the middle one is the default.</b> <i>When I talk to
    /// them</i> completes the handover the instant the player opens the dialogue
    /// with that customer — <see cref="HandoverPlace"/> carries the reading, and
    /// it is an event the game raises rather than a condition checked on a
    /// timer. <i>From anywhere</i> is what the build did before this option
    /// existed: the deal completes on the next sweep after its time arrives,
    /// wherever the player is.
    ///
    /// The line under <i>from anywhere</i> is a row of its own for the same
    /// reason the packaging footnote is: it has to be readable before the answer
    /// is chosen, not only after. What it cannot say in one line is why, and that
    /// is beside the option in <c>app.md</c> — the contract list is shared and a
    /// handover takes from the local player's own inventory, so a host on this
    /// answer reaches every contract on that list first, out of his own pockets,
    /// and a guest walking to the customer finds the deal gone whatever that
    /// guest's own copy was set to.
    ///
    /// The grade question sits a step further in, because it is a setting inside
    /// the switch that decides whether it applies: with the handover left on the
    /// game's own behaviour there is nothing for it to constrain, so it is not
    /// drawn at all. The footnote is a row of its own rather than a line under
    /// one answer, because it is true under both — exactness is about the grade,
    /// and packages cannot be split, so a delivery may be exact in its grade and
    /// still overshoot in units.
    ///
    /// It used to carry one row per live contract with the gate's own reason.
    /// The owner deleted it, and he was right: the game already lists his
    /// contracts and their times down the left of the screen, always visible,
    /// and a player would have to put that away and open a phone app to see the
    /// same thing again. <em>"Ich werde doch nie da reingucken."</em>
    ///
    /// What went with it is the gate's reason — <c>carrying 0 of 8</c>,
    /// <c>a dealer is assigned</c> — which the game does not show anywhere. A
    /// notification was offered as a replacement and refused: <em>"komplett raus
    /// ohne Ersatz"</em>. A later reader finding that gap is finding a decision,
    /// not a defect.
    /// </remarks>
    private static AutomationBlock Handover(Catalogue shop)
    {
        var claims = new[] { "AutoHandover", "HandoverFromAnywhere", "MayUseAHigherGrade" };

        AutomationSetting? handover = shop.Find("AutoHandover");
        AutomationSetting? anywhere = shop.Find("HandoverFromAnywhere");
        AutomationSetting? grade = shop.Find("MayUseAHigherGrade");

        var rows = new List<FormRow>(WhereAHandoverMayHappen(handover, anywhere));

        if (handover is null || !handover.SwitchedOn || grade is null)
        {
            return new AutomationBlock("HANDOVER", rows, claims, shop.HostOnly(claims));
        }

        rows.AddRange(TwoAnswers.Rows(grade, "Exactly the grade that was ordered", indent: 2));
        rows.Add(new FormRow(
            grade.Key + ":packages",
            FormRowKind.Notice,
            "* package sizes may still overshoot — packages cannot be split",
            indent: 2));

        return new AutomationBlock("HANDOVER", rows, claims, shop.HostOnly(claims));
    }

    /// <summary>
    /// The handover's own question: manual, at the customer, or from anywhere.
    /// Exactly one answer is ticked, and every answer writes both entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two keys and three answers, so the file can never hold a fourth.</b>
    /// <c>HandoverFromAnywhere</c> says where and <c>AutoHandover</c> says
    /// whether, and a press writes them together — which is what
    /// <see cref="FormRow.Press"/> is a list for. Writing only the one that
    /// changed would leave <i>manual</i> in the file beside <i>from
    /// anywhere</i>, a combination this page cannot draw and so, by
    /// <c>CLAUDE.md</c>'s rule, cannot be allowed to exist.
    /// </para>
    /// <para>
    /// The answers are built here rather than through <see cref="TwoAnswers"/>
    /// because that shape is one entry's two states, and this is one question
    /// over two entries. The row identities are still that shape's, so the app
    /// and its tests name a handover answer the way they name every other.
    /// </para>
    /// <para>
    /// A file hand-edited into the pair this page cannot write — the handover
    /// manual and <i>from anywhere</i> on — draws as <i>manual</i>, which is what
    /// the mod will then do: <see cref="HandoverGate"/> stands down on the switch
    /// before it ever asks where. So the page says what happens rather than what
    /// the file literally holds, and the next press puts the two back in step.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<FormRow> WhereAHandoverMayHappen(
        AutomationSetting? handover, AutomationSetting? anywhere)
    {
        // A build carrying neither has no handover to configure, and an
        // unanswerable question is worse than an absent one.
        if (handover is null || anywhere is null)
        {
            return Array.Empty<FormRow>();
        }

        bool automated = handover.SwitchedOn;
        bool fromAnywhere = automated && anywhere.SwitchedOn;

        FormRow Answer(string id, string text, bool chosen, bool on, bool anywhereValue) =>
            new(
                id,
                FormRowKind.Choice,
                text,
                chosen: chosen,
                indent: 1,
                press: new[]
                {
                    new SettingChange("AutoHandover", AutomationSetting.OnOff(on)),
                    new SettingChange("HandoverFromAnywhere", AutomationSetting.OnOff(anywhereValue)),
                });

        return new[]
        {
            Answer(
                TwoAnswers.No("AutoHandover"),
                TwoAnswers.TheGamesOwnBehaviour,
                chosen: !automated,
                on: false,
                anywhereValue: false),
            Answer(
                TwoAnswers.Yes("AutoHandover"),
                handover.Title,
                chosen: automated && !fromAnywhere,
                on: true,
                anywhereValue: false),
            Answer(
                TwoAnswers.Yes("HandoverFromAnywhere"),
                anywhere.Title,
                chosen: fromAnywhere,
                on: true,
                anywhereValue: true),

            // What that answer costs, in the one line the column has room for.
            // Both halves of it: whose goods go, and that this answer wins over
            // whatever the other players chose. It wins by construction — the
            // contract list is shared and a host reaching it from across the map
            // gets there first — so a guest on manual or on "when I talk to
            // them" still loses the delivery. The owner was shown that cost and
            // kept the answer with the warning on it; app.md carries the full
            // explanation beside the option, and the record of the decision.
            new FormRow(
                anywhere.Key + ":multiplayer",
                FormRowKind.Notice,
                "* in multiplayer the host's goods go, whatever others chose",
                indent: 2),
        };
    }

    /// <summary>
    /// The settings as the file holds them, by entry name, and the few shapes a
    /// block builds out of them.
    /// </summary>
    private sealed class Catalogue
    {
        private readonly Dictionary<string, AutomationSetting> _settings;

        public Catalogue(Dictionary<string, AutomationSetting> settings) => _settings = settings;

        public AutomationSetting? Find(string key) =>
            _settings.TryGetValue(key, out AutomationSetting? setting) ? setting : null;

        /// <summary>
        /// One setting as the question it is: two answers, exactly one ticked.
        /// </summary>
        /// <remarks>
        /// A setting this build does not carry is a feature left out of it. Its
        /// block then holds nothing, which is the honest picture: there is no
        /// such feature to configure, and an unanswerable question is worse than
        /// an absent one.
        /// </remarks>
        public IReadOnlyList<FormRow> Question(string key, int indent = 1)
        {
            AutomationSetting? setting = Find(key);

            return setting is null
                ? Array.Empty<FormRow>()
                : TwoAnswers.Rows(setting, TwoAnswers.TheGamesOwnBehaviour, indent);
        }

        /// <summary>Whether a setting this build carries is switched on.</summary>
        public bool IsOn(string key) => Find(key) is { SwitchedOn: true };

        /// <summary>
        /// Whether every setting a block answers for only does anything on the
        /// host — which is what decides whether the block is built on a guest.
        /// </summary>
        /// <remarks>
        /// Asked of the keys the block claims rather than of the rows it happens
        /// to be showing, because a setting hidden behind a switch that is off is
        /// still the block's and still the host's. A block this build carries no
        /// settings for answers false and is left out of a guest's page for
        /// having no rows instead.
        /// </remarks>
        public bool HostOnly(IReadOnlyList<string> claims)
        {
            var known = 0;

            foreach (string key in claims)
            {
                if (Find(key) is not AutomationSetting setting)
                {
                    continue;
                }

                if (!setting.HostOnly)
                {
                    return false;
                }

                known++;
            }

            return known > 0;
        }

        /// <summary>
        /// The chance floor: a number between two ends, with its own <c>−</c>
        /// and <c>+</c>, and typed into as well as stepped.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The two directions are the catalogue's, and either may be empty:
        /// that is what makes the ends blunt. At 50% the <c>−</c> writes
        /// nothing and at 100% the <c>+</c> writes nothing, rather than wrapping
        /// round to the other end, which is the opposite of what a player at
        /// either end asked for.
        /// </para>
        /// <para>
        /// The typed entry is not a convenience — the owner asked for it twice
        /// and in capitals. What it will take is <see cref="TypedEntry"/>, in
        /// the unit the row is drawn in: whole percent, because that is what the
        /// game shows the player and what this row says.
        /// </para>
        /// </remarks>
        public void Chance(List<FormRow> rows, int indent)
        {
            AutomationSetting? setting = Find(ChanceFloor.Key);
            if (setting is null)
            {
                return;
            }

            rows.Add(new FormRow(
                setting.Key,
                FormRowKind.Stepper,
                setting.Title,
                value: setting.Value,
                hint: setting.Summary,
                indent: indent,

                // The row's own button, where a build lends no stepper, steps
                // the way the rest of the app's rows do — one direction, and
                // round to the bottom from the top.
                //
                // That "round to the bottom" is not decoration. This used to be
                // the range's own Next, which is empty at 100%, so on a build
                // with no stepper the control stepped up only and then did
                // nothing at all: the floor could be changed no further except
                // by editing MelonPreferences.cfg. The two buttons of the real
                // stepper stay blunt at the ends, which is what app.md asks for
                // and what up and down below carry; a single button that cannot
                // come back is a different thing from a blunt end.
                press: Written(
                    setting.Change.Next.Length > 0
                        ? setting.Change.Next
                        : Figures.Setting(ChanceFloor.Lowest),
                    setting.Key),
                up: Written(setting.Change.Next, setting.Key),
                down: Written(setting.Change.Previous, setting.Key),
                typed: new TypedEntry(
                    setting.Key,
                    ChanceFloor.LowestTyped,
                    ChanceFloor.HighestTyped,
                    ChanceFloor.PerTypedUnit)));
        }

        /// <summary>
        /// One write, or none at all where the control has nothing to say in
        /// that direction.
        /// </summary>
        private static IReadOnlyList<SettingChange> Written(string value, string key) =>
            value.Length == 0
                ? Array.Empty<SettingChange>()
                : new[] { new SettingChange(key, value) };

        /// <summary>
        /// One deal window: its name, and the clock times the game reports for
        /// it. The hours are never written down — a patch that moves a window
        /// moves this line with it.
        /// </summary>
        public void Window(List<FormRow> rows, string key, int indent = 1)
        {
            AutomationSetting? setting = Find(key);
            if (setting is null)
            {
                return;
            }

            rows.Add(new FormRow(
                setting.Key,
                FormRowKind.Toggle,
                setting.Title,
                value: setting.Summary,
                chosen: setting.SwitchedOn,
                indent: indent,
                press: Next(setting)));
        }

        /// <summary>What activating a setting's own control writes, where it can.</summary>
        private static IReadOnlyList<SettingChange> Next(AutomationSetting setting) =>
            setting.Change.CanChange
                ? new[] { new SettingChange(setting.Key, setting.Change.Next) }
                : Array.Empty<SettingChange>();
    }
}
