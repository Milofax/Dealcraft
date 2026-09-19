using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class AutomationCatalogTests
{
    /// <summary>
    /// The row identities are the MelonPreferences entry names, so that a value
    /// edited in the file and a row read in the app are demonstrably the same
    /// setting. These literals are the config file's contract.
    /// </summary>
    [Fact]
    public void Every_row_is_named_after_its_preferences_entry()
    {
        Assert.Equal(
            new[]
            {
                "AutoCounterOffer", "AutoHandover", "HandoverFromAnywhere",
                "MayUseAHigherGrade", "AcceptanceProbabilityThreshold",
            },
            AutomationCatalog.Describe(new AdvisorSettings()).Select(item => item.Key));
    }

    [Fact]
    public void Each_switch_reports_the_state_it_was_given()
    {
        var settings = new AdvisorSettings { AutoCounterOffer = true, AutoHandover = false };

        Assert.Equal(
            new[] { "On", "Off" },
            AutomationCatalog.Describe(settings).Take(2).Select(item => item.Value));
    }

    /// <summary>
    /// The one row that is not the file's own text, and it is a decision rather
    /// than a slip. The file holds a chance, 0..1, because that is the unit the
    /// game's own figure is in and the unit every reader of the setting works
    /// in; the player is shown whole percent, because that is what the game
    /// shows them and what <c>app.md</c> draws. What a press writes back is the
    /// file's spelling, so the file is still the only store — and the
    /// preference's own description says which unit it holds, where somebody
    /// editing by hand will see it.
    /// </summary>
    [Fact]
    public void The_chance_is_shown_as_the_player_reads_it_and_written_as_the_file_holds_it()
    {
        IReadOnlyList<AutomationSetting> described = AutomationCatalog.Describe(
            new AdvisorSettings { AcceptanceProbabilityThreshold = 0.75f });

        Assert.Equal("75%", Value(described, "AcceptanceProbabilityThreshold"));
        Assert.Equal(
            "0.8",
            described.Single(item => item.Key == "AcceptanceProbabilityThreshold").Change.Next);
    }

    /// <summary>
    /// Section 4 of the redesign, held to rather than promised: the paragraphs
    /// that used to sit beside every setting are gone, and what replaced them is
    /// one line short enough to read at a glance. Where a control needs a
    /// paragraph, the control is wrong.
    /// </summary>
    [Fact]
    public void Every_row_has_a_title_a_value_and_at_most_one_short_line()
    {
        foreach (AutomationSetting item in AllRows())
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Title), $"{item.Key} has no title");
            Assert.False(string.IsNullOrWhiteSpace(item.Value), $"{item.Key} shows no value");
            Assert.True(
                item.Summary.Length <= AutomationSetting.SummaryLimit,
                $"{item.Key} says {item.Summary.Length} characters: \"{item.Summary}\"");
            Assert.DoesNotContain("\n", item.Summary);
        }
    }

    /// <summary>
    /// Every action on state the whole session shares goes out through a server
    /// RPC, so those rows only have an effect on the host. A row that claimed
    /// otherwise would be telling a guest their switch does something.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>AutoHandover</c> is deliberately not among them.</b> A handover is
    /// this player's goods leaving this player's pockets, and the game asks it
    /// that way — <c>Customer.IsReadyForHandover</c> reads the local player
    /// singleton, <c>Customer.ProcessHandover</c> is the client path a guest's
    /// own Done button takes. A guest who installs Dealcraft gets their own
    /// deliveries; the row would be lying if it said otherwise.
    /// </para>
    /// <para>
    /// <b>The chance floor is.</b> It was not, while it was a threshold the
    /// advisor overlay drew for whoever was looking at it. The overlay is gone
    /// and the one thing left that reads it is
    /// <c>CounterofferGate.Send</c> on the host, which a guest never reaches —
    /// so a guest's copy of this setting would refuse nothing, and the row would
    /// be telling them it does something.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_row_that_acts_on_state_the_session_shares_needs_the_host()
    {
        IReadOnlyList<AutomationSetting> described = AutomationCatalog.Describe(new AdvisorSettings());

        Assert.Equal(
            new[] { "AutoCounterOffer", "AcceptanceProbabilityThreshold" },
            described.Where(item => item.HostOnly).Select(item => item.Key));

        Assert.False(described.Single(item => item.Key == "AutoHandover").HostOnly);

        // And the handover's own sub-option follows the handover. A guest's
        // goods leave a guest's pockets, so the grade they leave in is theirs.
        Assert.False(described.Single(item => item.Key == "MayUseAHigherGrade").HostOnly);
    }

    /// <summary>
    /// The spec's rule, checked rather than promised: "there is no second store,
    /// no in-memory-only setting and no setting that exists in one place but not
    /// the other. If it can be configured, it is in the file and it is in the
    /// app."
    ///
    /// The file half of that lives in the Il2Cpp adapter, where no test may
    /// follow it, so the entry names are read out of its source. A setting
    /// declared anywhere in the adapter and not described by a catalogue fails
    /// here, and so does a catalogue row that no entry backs.
    ///
    /// The adapter is read whole rather than one named file, because a feature
    /// declares its own settings in its own file. That is what lets a new
    /// feature arrive without editing the shared settings class, and this guard
    /// has to follow it there or it would only be checking half the file.
    /// </summary>
    /// <remarks>
    /// <see cref="FileOnlySettings"/> is subtracted from the file's side. That
    /// is the one exemption the rule has, and subtracting it here rather than
    /// letting it slip past the regex is the difference between an exemption and
    /// a guard that stopped working.
    /// </remarks>
    [Fact]
    public void The_catalogues_list_exactly_the_entries_the_preferences_file_holds()
    {
        Assert.Equal(
            PreferenceEntryNames().Where(name => !FileOnlySettings.Keys.Contains(name)),
            DescribedKeys());
    }

    /// <summary>
    /// The exempted entry really is declared in the file, so the subtraction
    /// above is hiding something real rather than papering over a typo. If this
    /// setting were ever renamed or dropped, this fails and the exemption gets
    /// looked at again.
    /// </summary>
    [Fact]
    public void The_file_only_setting_is_actually_in_the_preferences_file()
    {
        foreach (string key in FileOnlySettings.Keys)
        {
            Assert.Contains(key, PreferenceEntryNames());
        }
    }

    /// <summary>
    /// And it is absent from the app in <em>both</em> directions. A setting the
    /// app can show but not change, or change but not show, is the half-wired
    /// state the "file and app" rule exists to prevent; an exemption that
    /// produced one would be worse than no exemption.
    /// </summary>
    [Fact]
    public void A_file_only_setting_is_absent_from_the_app_in_both_directions()
    {
        foreach (string key in FileOnlySettings.Keys)
        {
            Assert.DoesNotContain(key, AllRows().Select(row => row.Key));
        }

        foreach (string file in FileOnlySettings.Files)
        {
            string source = AdapterSource.Read(Path.Combine("Features", file));

            Assert.False(
                Regex.IsMatch(source, @"override\s+IReadOnlyList<AutomationSetting>\s+Describe"),
                $"{file} owns a file-only setting but describes rows to the app");
            Assert.False(
                Regex.IsMatch(source, @"override\s+bool\s+Change"),
                $"{file} owns a file-only setting but takes changes from the app");
        }
    }

    /// <summary>
    /// The other half of the spec's "both ways" rule: "a change made there
    /// writes straight back".
    ///
    /// There used to be an exception — a comma-separated list of customer names,
    /// which is free text the phone has nowhere to type. The list is deleted, so
    /// there is none: every setting the mod holds can be changed from the app.
    /// </summary>
    [Fact]
    public void Every_setting_can_be_changed_from_the_app()
    {
        Assert.DoesNotContain(AllRows(), row => !row.Change.CanChange);
    }

    /// <summary>
    /// A ladder whose only rung is where the setting already stands would give
    /// a row that looks changeable and does nothing when pressed.
    /// </summary>
    [Fact]
    public void A_changeable_row_always_offers_a_value_other_than_the_one_it_shows()
    {
        foreach (AutomationSetting row in AllRows().Where(row => row.Change.CanChange))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(row.Change.Next),
                $"{row.Key} says it can change but offers no value");
            Assert.True(
                row.Change.Next != row.Value,
                $"{row.Key} shows {row.Value} and offers {row.Change.Next}, which changes nothing");
        }
    }

    /// <summary>
    /// Every row the app's Automation section can show, from every catalogue.
    /// One line per catalogue: a feature with settings of its own brings one.
    /// </summary>
    /// <remarks>
    /// <c>PriceMaintenanceCatalog</c> was missing here while its key was also
    /// missing from the file's side, so the guard below passed by subtracting the
    /// same setting from both lists. Both halves are fixed; this is the half that
    /// makes the doc comment above true.
    /// </remarks>
    private static IEnumerable<AutomationSetting> AllRows() =>
        AutomationCatalog.Describe(new AdvisorSettings())
            .Concat(DealWindowCatalog.Describe(new DealWindowSet(), Array.Empty<DealWindowHours>()))
            .Concat(PriceMaintenanceCatalog.Describe(maintaining: false));

    private static IEnumerable<string> DescribedKeys() =>
        AllRows().Select(item => item.Key).OrderBy(key => key, StringComparer.Ordinal);

    private static string Value(IEnumerable<AutomationSetting> described, string key) =>
        described.Single(item => item.Key == key).Value;

    /// <summary>
    /// Every name passed to <c>category.CreateEntry</c> anywhere in the adapter.
    /// Read by <see cref="PreferencesFile"/>, which is the one place that scrapes
    /// the adapter for keys — a second copy of this with a different regex is how
    /// the last blind spot got in.
    /// </summary>
    private static IEnumerable<string> PreferenceEntryNames() => PreferencesFile.EntryNames();
}
