using System;
using System.IO;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// A released build keeps no diagnostic files, and it is the build that decides
/// rather than a switch somebody has to remember.
/// </summary>
/// <remarks>
/// <para>
/// The two diagnostic files found every real defect this project has had,
/// including five that 767 passing tests did not. So the machine the mod is
/// worked on records; a player who installed it to play a game does not, and
/// should not be writing a file every time a customer says no.
/// </para>
/// <para>
/// Made a property of the build on purpose. A setting left in the wrong
/// position before a release is exactly the kind of thing that ships, and there
/// is no position here to leave anything in: <c>bin/install-dealcraft</c> passes
/// <c>-p:DealcraftDebug=true</c> and <c>bin/publish-dealcraft</c> has no way to.
/// These guards hold both halves of that.
/// </para>
/// </remarks>
public class AReleaseBuildRecordsNothingTests
{
    [Fact]
    public void The_recording_is_decided_by_the_build_and_not_by_a_setting()
    {
        string source = AdapterSource.Read("Diagnostics.cs");

        Assert.Contains("#if DEALCRAFT_DEBUG", source, StringComparison.Ordinal);
        Assert.Contains("Recording = true", source, StringComparison.Ordinal);
        Assert.Contains("Recording = false", source, StringComparison.Ordinal);

        // No preferences key of its own, in either direction. The decision
        // record has none at all, which is the rule CLAUDE.md states about keys
        // the interface cannot account for.
        Assert.DoesNotContain("CreateEntry", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_decision_record_opens_nothing_on_a_build_that_does_not_record()
    {
        string source = AdapterSource.Read("Features/DebugRecordFeature.cs");

        int guarded = source.IndexOf("!Diagnostics.Recording", StringComparison.Ordinal);
        int opened = source.IndexOf("new LedgerFile(", StringComparison.Ordinal);

        Assert.True(guarded >= 0, "DebugRecordFeature no longer asks whether this build records.");
        Assert.True(
            guarded < opened,
            "DebugRecordFeature must return before it opens the file, not after: a folder made "
            + "and a file created is already a mark on a player's machine.");
    }

    /// <summary>
    /// The handover ledger follows the build too, and keeps its file-only key on
    /// top — it is the one instrument a player might be asked to switch on for a
    /// bug report, and a released build can still do that.
    /// </summary>
    [Fact]
    public void The_handover_ledger_defaults_to_whatever_the_build_does()
    {
        string source = AdapterSource.Read("Features/HandoverLedgerFeature.cs");

        Assert.Contains("Key, Diagnostics.Recording", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the release path cannot turn it on. This is the half that matters:
    /// everything above is undone by one flag in the wrong script.
    /// </summary>
    /// <remarks>
    /// Silent where the scripts are not there. They live in the coordination
    /// repository and the published one is built from <c>src/Dealcraft</c>
    /// alone, so from a clean copy there is no release path to guard — and the
    /// clean copy is exactly where <c>bin/publish-dealcraft</c> runs the suite
    /// before it ships. A guard that fails for being unable to find its subject
    /// would refuse every release.
    /// </remarks>
    [Fact]
    public void The_publish_script_never_asks_for_a_recording_build()
    {
        string? publish = Script("publish-dealcraft");
        string? install = Script("install-dealcraft");

        if (publish is null || install is null)
        {
            return;
        }

        Assert.DoesNotContain("DealcraftDebug=true", publish, StringComparison.Ordinal);
        Assert.Contains("DealcraftDebug=true", install, StringComparison.Ordinal);
    }

    /// <summary>
    /// A script in <c>bin/</c>, found by walking up from the test assembly the
    /// way the adapter and the scenarios are, or null where this checkout has
    /// no such script.
    /// </summary>
    private static string? Script(string name)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "bin", name);

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        return null;
    }
}
