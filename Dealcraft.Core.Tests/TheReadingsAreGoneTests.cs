using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The page answers questions. It reports nothing, counts nothing and keeps no
/// record, and none of the three comes back.
/// </summary>
/// <remarks>
/// <para>
/// Shown the interface whole for the first time, the owner deleted every status
/// word — <i>"Ich gehe davon aus, wenn ich es gesetzt habe, dann läuft es."</i> —
/// every counter and last-action row — they <i>"answer a question I did not
/// ask"</i> — and the bonus breakdown of the last handover: <i>"Interessiert
/// niemanden."</i> With them the second input went too: the form is now a
/// function of the settings alone.
/// </para>
/// <para>
/// The gates still decide, still word their reasons and still say why nothing
/// happened. Every one of those sentences goes to the debug file, which is where
/// a later reader should look for them rather than putting them back on screen.
/// </para>
/// <para>
/// A deletion stays deleted only if something fails when it comes back, which is
/// <see cref="TheTwoTabsAreGoneTests"/>'s pattern and the reason this is a test
/// rather than a note.
/// </para>
/// </remarks>
public class TheReadingsAreGoneTests
{
    /// <summary>
    /// The model the readings were carried in. By reflection over the core
    /// assembly, because this is the half a test can ask directly.
    /// </summary>
    [Fact]
    public void Nothing_in_the_core_carries_a_reading_any_more()
    {
        string[] gone =
        {
            "AutomationReadings", "BlockVerdict", "HandoverRecord", "HandoverBonus",
        };

        Assert.DoesNotContain(
            gone, name => typeof(AutomationForm).Assembly.GetType($"Dealcraft.Core.{name}") is not null);
    }

    /// <summary>
    /// <c>Build</c> lost the readings, and that is the measure of how much came
    /// off the page: with no status words, no counters and no lists, the page is
    /// a function of the preferences file and of whose machine it is on.
    /// </summary>
    /// <remarks>
    /// <b>Narrowed rather than deleted, the way ticket 34's guard was.</b> It
    /// used to assert that <c>Build</c> takes one parameter, and that is not what
    /// it was protecting: what the owner deleted was the readings — the status
    /// word per block, the count per pass, the last handover's bonuses — and the
    /// second input they arrived in. The authority is not one of those. It says
    /// which machine the page is being drawn on, which decides whether a block is
    /// built at all, and it reports nothing about what the mod has been doing. So
    /// what is asserted is the two parameters by name, and a third would still
    /// fail here.
    /// </remarks>
    [Fact]
    public void The_page_is_built_from_the_settings_and_from_whose_machine_it_is_on()
    {
        MethodInfo build = Assert.Single(
            typeof(AutomationForm).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == "Build");

        Assert.Equal(
            new[] { typeof(IReadOnlyList<AutomationSetting>), typeof(ServerAuthority) },
            build.GetParameters().Select(parameter => parameter.ParameterType));
    }

    /// <summary>
    /// A block heading has nothing on its right. It used to carry the word its
    /// own gate returned, shortened, and that word is what the owner deleted
    /// first.
    /// </summary>
    /// <remarks>
    /// <c>HostOnly</c> is beside them and is not a reading of that kind: it says
    /// nothing about what the block has been doing, only whether the settings it
    /// answers for do anything anywhere but the host. A verdict, a count or a
    /// last action appearing here still fails.
    /// </remarks>
    [Fact]
    public void A_block_is_a_title_some_rows_the_settings_it_claims_and_whose_they_are()
    {
        Assert.Equal(
            new[] { "Title", "Rows", "Claims", "HostOnly" },
            typeof(AutomationBlock)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name));
    }

    /// <summary>
    /// The contract row went with ticket 34's handover board and the row kind
    /// went with the readings: there is nothing left for the page to draw one
    /// contract of, so there is no kind of row that means one.
    /// </summary>
    [Fact]
    public void There_is_no_kind_of_row_that_means_one_live_contract()
    {
        Assert.DoesNotContain("Contract", Enum.GetNames<FormRowKind>());
    }

    /// <summary>
    /// And the adapter that fed the readings is gone with them: the reader that
    /// kept the deal-completion popup's bonuses, and the call that gathered every
    /// gate's sentence for the page.
    /// </summary>
    [Fact]
    public void Nothing_in_the_adapter_still_reads_the_game_for_the_page()
    {
        string[] gone =
        {
            "AutomationReadings", "BlockVerdict", "HandoverRecord", "HandoverBonus",
            "LastHandover", "ReadGates",
        };

        var named = new List<string>();

        foreach (string path in AdapterSource.Files())
        {
            string file = Path.GetFileName(path);
            int number = 0;

            foreach (string raw in File.ReadLines(path))
            {
                number++;
                string text = raw.Trim();

                if (text.StartsWith("//", StringComparison.Ordinal)
                    || text.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                named.AddRange(gone
                    .Where(name => text.Contains(name, StringComparison.Ordinal))
                    .Select(name => $"{file}:{number} names {name}"));
            }
        }

        Assert.Empty(named);
    }

    /// <summary>
    /// The save guard is not a control, and the way to know is that the
    /// preferences file has no entry for it.
    /// </summary>
    /// <remarks>
    /// <i>"Ich soll doch nicht entscheiden, was jetzt irgendwie sauber
    /// abläuft."</i> The behaviour stays: <c>LifecycleFeature</c> hands the watch
    /// a guard that is always on. What left is the question, and a question a
    /// player should never have been handed leaves the file as well as the
    /// screen — a key the interface cannot account for is a defect.
    /// </remarks>
    [Fact]
    public void The_save_guard_is_in_the_code_and_in_neither_the_app_nor_the_file()
    {
        Assert.DoesNotContain(
            "PauseAutomationWhileSaving", AdapterSource.ReadAll(), StringComparison.Ordinal);

        Assert.Null(typeof(AutomationForm).Assembly.GetType("Dealcraft.Core.LifecycleCatalog"));
    }

    /// <summary>
    /// And the gate has no second condition beside the saving flag. While one
    /// existed the guard could be switched off, which is what made it a
    /// question; with it gone a save holds every pass and nothing can say
    /// otherwise.
    /// </summary>
    [Fact]
    public void A_save_in_progress_holds_every_pass_and_nothing_can_wave_it_through()
    {
        Assert.DoesNotContain(
            typeof(LifecycleConditions).GetProperties(),
            property => property.Name == "WaitWhileSaving");

        LifecycleVerdict verdict = LifecycleGate.Consider(new LifecycleConditions
        {
            Pass = LifecyclePass.Scheduling,
            Enabled = true,
            Shared = true,
            Authority = ServerAuthority.Held,
            SaveLoaded = true,
            Saving = true,
        });

        Assert.Equal(LifecycleOutcome.Wait, verdict.Outcome);
        Assert.Contains("the game is saving", verdict.Reason, StringComparison.Ordinal);
    }
}
