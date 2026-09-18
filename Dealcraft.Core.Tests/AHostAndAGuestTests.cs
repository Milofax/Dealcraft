using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The page knows whose machine it is on: four blocks on the host, one block and
/// a note on a guest.
/// </summary>
/// <remarks>
/// <para>
/// <c>AutomationSetting.HostOnly</c> has been on every setting since they were
/// written and <b>no line of the interface ever drew it</b>. A guest could set
/// the listed price, the negotiation and the schedule, and not one of the three
/// did anything on his machine — the same defect as the counteroffer overlay and
/// the handover board, one layer down: a control that cannot do what it appears
/// to offer.
/// </para>
/// <para>
/// <b>Nothing is sent either way.</b> The host's settings are not shown to a
/// guest, because showing them means the host sending them, which means a network
/// message a vanilla client does not have — <c>CLAUDE.md</c>'s first rule, and the
/// one that keeps an unmodified friend able to join at all. The note says instead
/// that the three are set on the host and are working for the guest's deals, and
/// that second sentence is the one that matters: without it a guest reads his
/// short page as a quarter of the mod.
/// </para>
/// </remarks>
public class AHostAndAGuestTests
{
    /// <summary>
    /// A guest's page: the note, then the handover block, and nothing else. The
    /// three blocks that act on state the session shares are not built at all.
    /// </summary>
    [Fact]
    public void A_guests_page_is_the_note_and_the_handover_block()
    {
        AutomationForm guest = Form(ServerAuthority.Guest);

        Assert.Equal(new[] { string.Empty, "HANDOVER" }, guest.Blocks.Select(block => block.Title));

        Assert.Equal(
            new[] { "LISTED PRICE", "PRICE NEGOTIATION", "ACCEPTED SCHEDULE" },
            Form(ServerAuthority.Held).Blocks
                .Select(block => block.Title)
                .Except(guest.Blocks.Select(block => block.Title), StringComparer.Ordinal));
    }

    /// <summary>
    /// And a host's is the four the drawing carries, with no note among them: he
    /// is the machine the three act on, so there is nothing to tell him about
    /// somebody else's.
    /// </summary>
    [Fact]
    public void A_hosts_page_is_the_four_blocks_and_carries_no_note()
    {
        AutomationForm host = Form(ServerAuthority.Held);

        Assert.Equal(
            new[] { "LISTED PRICE", "PRICE NEGOTIATION", "ACCEPTED SCHEDULE", "HANDOVER" },
            host.Blocks.Select(block => block.Title));

        Assert.DoesNotContain(
            host.Blocks.SelectMany(block => block.Rows),
            row => row.Id.StartsWith(HostOnlyNote.Key, StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>HostOnly</c> is what decides, rather than a list of block names beside
    /// the settings that could drift from them. Three of the four say yes; the
    /// handover says no, and always did.
    /// </summary>
    [Fact]
    public void The_host_only_flag_is_what_decides_whether_a_block_is_built()
    {
        Assert.Equal(
            new[] { true, true, true, false },
            Form(ServerAuthority.Held).Blocks.Select(block => block.HostOnly));
    }

    /// <summary>
    /// The same thing said from the guest's side: nothing on his page belongs to
    /// a block that only works on the host.
    /// </summary>
    /// <remarks>
    /// Asked of the block rather than of the row. <c>FormRow</c> carried a
    /// <c>HostOnly</c> of its own, copied down from the setting, and this was the
    /// only line in the repository that ever read it — a flag written in four
    /// places and read by one test, which is the shape ticket 40's own headline
    /// was about. It is deleted; the block is what decides.
    /// </remarks>
    [Fact]
    public void Nothing_on_a_guests_page_is_a_control_that_only_works_on_the_host()
    {
        Assert.All(
            Form(ServerAuthority.Guest).Blocks,
            block => Assert.False(block.HostOnly, block.Title));
    }

    /// <summary>
    /// A guest's page answers for the handover's three keys and for nothing
    /// else. The other five are the host's, and a guest's copy of them would
    /// change nothing on his machine — which is the defect this ticket removes
    /// rather than a setting that has gone missing from the app.
    /// </summary>
    [Fact]
    public void A_guests_page_answers_for_the_handovers_settings_only()
    {
        Assert.Equal(
            new[] { "AutoHandover", "HandoverFromAnywhere", "MayUseAHigherGrade" },
            Form(ServerAuthority.Guest).Claimed.OrderBy(key => key, StringComparer.Ordinal));
    }

    /// <summary>
    /// And the handover is his to set: where a handover may happen and which
    /// grade goes out both write, on his machine, for his own deliveries.
    /// </summary>
    /// <remarks>
    /// The where question writes two entries with one press, because it is three
    /// answers over two keys — see <c>AutomationForm.WhereAHandoverMayHappen</c>.
    /// </remarks>
    [Fact]
    public void The_handover_block_still_works_on_a_guest()
    {
        AutomationForm guest = Form(ServerAuthority.Guest, AllOn());

        Assert.Equal(
            new[] { "On", "Off" },
            guest.Press(TwoAnswers.Yes("AutoHandover")).Select(change => change.Value));
        Assert.Equal(
            new[] { "On", "On" },
            guest.Press(TwoAnswers.Yes("HandoverFromAnywhere")).Select(change => change.Value));
        Assert.Equal(
            "MayUseAHigherGrade",
            Assert.Single(guest.Press(TwoAnswers.Yes("MayUseAHigherGrade"))).Key);
    }

    /// <summary>
    /// A row that is not on his page writes nothing if something presses its key
    /// anyway, which is what "not built" means rather than "drawn and ignored".
    /// </summary>
    [Theory]
    [InlineData("MaintainListedPrices")]
    [InlineData("AutoCounterOffer")]
    public void A_guest_pressing_one_of_the_hosts_controls_writes_nothing(string key)
    {
        AutomationForm guest = Form(ServerAuthority.Guest, AllOn());

        Assert.Empty(guest.Press(TwoAnswers.No(key)));
        Assert.Empty(guest.Press(TwoAnswers.Yes(key)));
        Assert.Empty(guest.Press("AllowMorningWindow"));
        Assert.Empty(guest.Step(ChanceFloor.Key, up: true));
        Assert.Empty(guest.Type(ChanceFloor.Key, "75"));
    }

    // --- the note ------------------------------------------------------------

    /// <summary>
    /// The note, word for word as <c>app.md</c> draws it, and both sentences of
    /// it. <b>The second is the one that matters</b>: without it a guest with one
    /// block on his page concludes he was given a quarter of the mod, when the
    /// other three are working for his deals on somebody else's machine.
    /// </summary>
    [Fact]
    public void The_note_names_the_three_and_says_they_apply_to_his_deals_too()
    {
        Assert.Equal(
            "Listed price, price negotiation and accepted schedule can only be set "
                + "on the host. They apply to your deals too.",
            string.Join(" ", Note().Select(row => row.Text)));
    }

    /// <summary>
    /// <b>A list of lines, not a string.</b> Every row of this page is asserted
    /// at 63 characters or fewer — the column the game reports at the app's own
    /// font size — and the note is 111 over its two rows. One string would fail
    /// that on the first build.
    /// </summary>
    /// <remarks>
    /// <c>app.md</c> and ticket 40 both say 113. Counted, the two lines are 57
    /// and 54, which is 111 as rows and 112 as one sentence with the space
    /// between them. The number in the spec is wrong; the lines it draws are what
    /// this asserts.
    /// </remarks>
    [Fact]
    public void The_note_is_a_list_of_lines_and_every_one_fits_the_column()
    {
        const int column = 63;

        IReadOnlyList<FormRow> note = Note();

        Assert.Equal(2, note.Count);
        Assert.Equal(111, note.Sum(row => row.Text.Length));
        Assert.All(
            note,
            row => Assert.True(
                row.Text.Length + row.Value.Length <= column,
                $"{row.Id} is {row.Text.Length + row.Value.Length} characters wide"));
    }

    /// <summary>
    /// It is something the page says rather than something it asks: no press, no
    /// tick, nothing to type into, and no indent — it sits on the page's own
    /// margin rather than under a control.
    /// </summary>
    [Fact]
    public void The_note_asks_nothing_and_writes_nothing()
    {
        Assert.All(Note(), row =>
        {
            Assert.Equal(FormRowKind.Notice, row.Kind);
            Assert.Empty(row.Press);
            Assert.Empty(row.Up);
            Assert.Empty(row.Down);
            Assert.False(row.Typed.Takes);
            Assert.Equal(0, row.Indent);
            Assert.False(row.Chosen);
        });
    }

    /// <summary>
    /// <b>And it does not carry the host's settings, which was decided against
    /// rather than forgotten.</b> Showing them means sending them, and a vanilla
    /// client has no message for that. Inferring them from what a guest can see is
    /// guessing, and a guessed setting on screen is worse than an absent one.
    /// </summary>
    [Fact]
    public void The_note_says_nothing_about_what_the_host_has_set()
    {
        string said = string.Join(" ", Rows(Form(ServerAuthority.Guest, AllOn())).Select(row => row.Text));

        Assert.DoesNotContain("90%", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Automated setting in the Price App", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Automated handling of customer requests", said, StringComparison.Ordinal);
    }

    // --- the title bar -------------------------------------------------------

    /// <summary>
    /// Stories 37, 38 and 39: the title bar says which machine the page belongs
    /// to, so a guest can tell at a glance why his is shorter.
    /// </summary>
    /// <remarks>
    /// <b>Solo play is <c>HOST MODE</c>.</b> <c>ServerAuthorityReader</c> answers
    /// server-and-client for a single player, which is what a single player is.
    /// </remarks>
    [Theory]
    [InlineData(ServerAuthority.Held, "HOST MODE")]
    [InlineData(ServerAuthority.WithoutAClient, "HOST MODE")]
    [InlineData(ServerAuthority.Guest, "CLIENT MODE")]
    [InlineData(ServerAuthority.NotTheServer, "CLIENT MODE")]
    public void The_title_bar_says_whose_machine_the_page_is_on(
        ServerAuthority authority, string mode)
    {
        Assert.Equal(mode, Form(authority).Mode);
    }

    /// <summary>
    /// A reading that could not be taken draws a guest's page, which is the same
    /// safe end every pass takes: a machine that cannot say it is the server does
    /// not act as one. It costs nothing that lasts — the page is rebuilt from a
    /// fresh reading every time the app is opened and after every write.
    /// </summary>
    [Fact]
    public void A_reading_that_failed_draws_a_guests_page_rather_than_the_hosts()
    {
        Assert.Equal(
            new[] { string.Empty, "HANDOVER" },
            Form(ServerAuthority.NotTheServer).Blocks.Select(block => block.Title));
    }

    // --- the far side of the seam --------------------------------------------

    /// <summary>
    /// The page is given the answer the passes already use, rather than one
    /// worked out for it. Read out of the adapter's source, because that side of
    /// the seam calls into the running game and no test here may follow it.
    /// </summary>
    [Fact]
    public void The_page_is_given_the_reading_every_pass_already_asks_for()
    {
        Assert.Contains(
            "ServerAuthorityReader.Read",
            AdapterSource.Read(Path.Combine("Features", "PhoneAppFeature.cs")),
            StringComparison.Ordinal);

        Assert.Contains(
            "AutomationForm.Build(_automation(), _authority())",
            AdapterSource.Read(Path.Combine("Phone", "AppPageView.cs")),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>And nothing is sent anywhere.</b> The host's settings reaching a guest
    /// would take a network message a vanilla client does not have, which is
    /// <c>CLAUDE.md</c>'s first rule and the thing that keeps an unmodified friend
    /// able to join. No RPC, no network object, no synced field: the app reads a
    /// local preferences file and asks FishNet one question about this machine.
    /// </summary>
    [Fact]
    public void Nothing_the_app_draws_sends_anything_anywhere()
    {
        string[] banned = { "Rpc", "NetworkObject", "SyncVar", "Broadcast" };

        var found = new List<string>();

        foreach (string path in AdapterSource.Files())
        {
            string file = Path.GetFileName(path);
            if (!path.Contains($"{Path.DirectorySeparatorChar}Phone{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                && file != "PhoneAppFeature.cs")
            {
                continue;
            }

            int number = 0;
            foreach (string raw in File.ReadLines(path))
            {
                number++;
                string text = raw.Trim();

                // Comments are where the disassembly is recorded, and the
                // vanilla RPCs it names are the ones this mod deliberately does
                // not add to.
                if (text.StartsWith("//", StringComparison.Ordinal)
                    || text.StartsWith("///", StringComparison.Ordinal)
                    || text.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                found.AddRange(banned
                    .Where(word => text.Contains(word, StringComparison.Ordinal))
                    .Select(word => $"{file}:{number} names {word}"));
            }
        }

        Assert.Empty(found);
    }

    /// <summary>
    /// The app will not even copy something networked: a clone carrying a
    /// <c>NetworkBehaviour</c> would be a network object the mod introduced and a
    /// component FishNet never indexed, so the copy is refused instead.
    /// </summary>
    [Fact]
    public void The_app_refuses_to_copy_anything_networked()
    {
        Assert.Contains(
            "GetComponentsInChildren<NetworkBehaviour>",
            AdapterSource.Read(Path.Combine("Phone", "PhoneApp.cs")),
            StringComparison.Ordinal);
    }

    // --- helpers -------------------------------------------------------------

    private static AdvisorSettings AllOn() => new()
    {
        AutoCounterOffer = true,
        AutoHandover = true,
        MayUseAHigherGrade = true,
    };

    // HandoverFromAnywhere stays off here on purpose: this file is about which
    // blocks a guest is given, and the careful answer is the one a fresh
    // install has.

    private static IReadOnlyList<FormRow> Note() =>
        Form(ServerAuthority.Guest).Blocks.First(block => block.Title.Length == 0).Rows;

    private static IEnumerable<FormRow> Rows(AutomationForm form) =>
        form.Blocks.SelectMany(block => block.Rows);

    private static AutomationForm Form(ServerAuthority authority, AdvisorSettings? settings = null)
    {
        var allowed = new DealWindowSet();
        allowed.Allow(DealWindow.Afternoon, true);

        return AutomationForm.Build(
            AutomationCatalog.Describe(settings ?? new AdvisorSettings())
                .Concat(DealWindowCatalog.Describe(allowed, Array.Empty<DealWindowHours>()))
                .Concat(PriceMaintenanceCatalog.Describe(maintaining: true))
                .ToArray(),
            authority);
    }
}
