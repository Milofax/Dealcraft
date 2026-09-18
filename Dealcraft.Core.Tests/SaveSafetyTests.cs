using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Two promises the spec makes about the game's save file, checked rather than
/// asserted.
///
/// "The mod stores nothing in the game's save" and "never act while
/// <c>SaveManager.IsSaving</c>" are both about code that calls into the running
/// game, and no test here may reference an Il2Cpp type. So the adapter is read
/// as text, the same way
/// <see cref="SearchProbeTests.No_adapter_calls_the_rolled_verdict"/> already
/// reads it. A guard like this cannot prove the save file is untouched — only a
/// byte-for-byte comparison in a running game can — but it can prove the mod
/// never asks to touch it, which is the part that can go wrong by accident.
/// </summary>
public class SaveSafetyTests
{
    /// <summary>
    /// Everything the game's persistence layer offers for <em>writing</em>, plus
    /// the plain .NET ways to reach a file. A mod that names none of these
    /// cannot put anything in a save, and a save with nothing of ours in it is a
    /// save that loads in vanilla when the mod is removed.
    ///
    /// <c>SaveManager</c> is not in this list: the mod does read it, for
    /// <c>IsSaving</c>, and the test below pins down that this is the only thing
    /// it reads.
    /// </summary>
    private static readonly string[] WaysToWriteAFile =
    {
        "ISaveable",
        "Saveable",
        "SaveData",
        "SaveFile",
        "SaveGame",
        "WriteFile",
        "System.IO",
        "StreamWriter",
        "FileStream",
        "File.Write",
        "File.Create",
        "File.Append",
        "Directory.Create",
    };

    /// <summary>
    /// The one file allowed to write anything at all, and what it is allowed to
    /// write. The handover ledger keeps a file of measurements; it is not a save
    /// and it is nowhere near one. Naming the exception here rather than
    /// loosening the rule is what keeps the rule worth having — everything the
    /// file may do is pinned down by
    /// <see cref="The_one_file_that_writes_anything_writes_only_its_own_ledger"/>.
    /// </summary>
    private const string TheOneFileThatWrites = "LedgerFile.cs";

    /// <summary>
    /// The mod registers nothing with the game's persistence layer, and opens no
    /// file of its own but one. Nothing of Dealcraft's is in the save, so
    /// removing the mod leaves a save with nothing to miss.
    /// </summary>
    [Fact]
    public void No_adapter_can_write_into_the_games_save()
    {
        var offenders = new List<string>();

        foreach ((string file, int line, string text) in AdapterLines())
        {
            if (file == TheOneFileThatWrites)
            {
                continue;
            }

            foreach (string way in WaysToWriteAFile)
            {
                if (text.Contains(way, StringComparison.Ordinal))
                {
                    offenders.Add($"{file}:{line} names {way}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The exception, held to its terms. The ledger's file lives under
    /// MelonLoader's own <c>UserData</c> directory, which is the game's answer
    /// to where a mod puts its files — so the path is never this machine's, and
    /// never anywhere near the save folder. A save is the one thing in this game
    /// that cannot be replaced.
    /// </summary>
    [Fact]
    public void The_one_file_that_writes_anything_writes_only_its_own_ledger()
    {
        string source = AdapterSource.Read(TheOneFileThatWrites);

        // Where it writes: MelonLoader's, and nothing else that resolves a path.
        Assert.Contains("MelonEnvironment.UserDataDirectory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Application.persistentDataPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.GetFolderPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AppData", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Saves", source, StringComparison.Ordinal);

        // What it writes: appends lines, and never opens anything for deletion
        // or truncation. A bug here could not take a file with it.
        Assert.DoesNotContain("File.Delete", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FileMode.Create", source, StringComparison.Ordinal);

        // And it cannot take a handover down with it: everything is wrapped and
        // the failure is reported rather than thrown. Read off the code rather
        // than the whole file, because the comments here explain at length why
        // nothing is thrown, and a reason for not doing something must not read
        // as doing it.
        Assert.Contains("catch (Exception", source, StringComparison.Ordinal);

        foreach ((string _, int line, string text) in
                 Lines(Path.Combine(AdapterSource.Directory, TheOneFileThatWrites)))
        {
            Assert.False(
                Regex.IsMatch(text, @"\bthrow\b"),
                $"{TheOneFileThatWrites}:{line} throws, and it is called from inside the game's "
                + "handover path");
        }
    }

    /// <summary>
    /// No other adapter file may resolve a path of its own. The ledger goes
    /// through the one file above, so there is one answer to "where does
    /// Dealcraft write" rather than one per feature.
    /// </summary>
    [Fact]
    public void Only_that_file_knows_where_anything_is_written()
    {
        var offenders = new List<string>();

        foreach ((string file, int line, string text) in AdapterLines())
        {
            if (file != TheOneFileThatWrites
                && text.Contains("MelonEnvironment", StringComparison.Ordinal))
            {
                offenders.Add($"{file}:{line}");
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The one thing the mod does want from the game's persistence layer is to
    /// be told when to keep out of the way. Reading a flag is not writing a
    /// save, and pinning the members down is what keeps that exemption honest:
    /// anything else reached for on those managers — a save triggered, a slot
    /// written, a load forced — is a failure rather than a judgement call.
    ///
    /// One file reads them, which the test below pins down, and in that file both
    /// are held in a local called <c>manager</c> — so every member the mod takes
    /// off either is a <c>manager.X</c> there, and those are what this collects.
    /// </summary>
    [Fact]
    public void The_only_things_read_off_the_persistence_managers_are_two_flags()
    {
        var read = new SortedSet<string>(StringComparer.Ordinal);

        foreach ((string _, int _, string text) in
                 Lines(Path.Combine(AdapterSource.Directory, "SaveStateReader.cs")))
        {
            foreach (Match match in Regex.Matches(text, @"\bmanager\.(?<member>\w+)"))
            {
                read.Add(match.Groups["member"].Value);
            }
        }

        Assert.Equal(new[] { "IsGameLoaded", "IsSaving" }, read);
    }

    /// <summary>
    /// And they are read in one place, so the two flags above really are the
    /// whole of the mod's dealings with the save system rather than one file's.
    /// </summary>
    [Fact]
    public void One_file_reads_the_persistence_managers()
    {
        var files = new List<string>();

        foreach ((string file, int _, string text) in AdapterLines())
        {
            if (text.Contains("SaveManager", StringComparison.Ordinal)
                || text.Contains("LoadManager", StringComparison.Ordinal))
            {
                files.Add(file);
            }
        }

        Assert.Equal(new[] { "SaveStateReader.cs" }, files.Distinct());
    }

    /// <summary>
    /// The rule <c>docs/native-truth.md</c> records, checked where it is easy to
    /// forget: a pass that sends one of the game's acting calls must have asked
    /// the lifecycle gate first. Three files send one, and this finds them by
    /// the call rather than by a list written down — a fourth automation added
    /// later is caught by the same test without anyone remembering to add it.
    ///
    /// Asking is spelled <c>LifecycleWatch</c> at the call sites, because the
    /// watch is the one route to the gate — which the test below pins down.
    /// </summary>
    [Fact]
    public void Every_pass_that_acts_on_the_world_consults_the_lifecycle_gate()
    {
        string[] actingCalls = { "PlayerAcceptedContract", "SendCounteroffer", "ProcessHandover" };
        var ungated = new List<string>();

        foreach (string file in AdapterSource.Files())
        {
            if (Path.GetFileName(file) == TheOneFileThatOnlyListens)
            {
                continue;
            }

            bool acts = Lines(file).Any(entry =>
                actingCalls.Any(call => entry.Text.Contains(call, StringComparison.Ordinal)));

            if (acts && !AsksTheGate(file))
            {
                ungated.Add(Path.GetFileName(file));
            }
        }

        Assert.Empty(ungated);
    }

    /// <summary>
    /// The one file that names an acting call without asking the gate, because
    /// it does not act: it patches those methods so as to watch them go past.
    /// Held to that by
    /// <see cref="The_file_that_only_listens_never_calls_what_it_listens_to"/>.
    /// </summary>
    private const string TheOneFileThatOnlyListens = "HandoverLedgerPatches.cs";

    /// <summary>
    /// The exemption above, held to its terms. The ledger's business with the
    /// handover path is entirely <c>harmony.Patch</c>: it installs a prefix and
    /// a postfix and reads their arguments. It never invokes what it patches, so
    /// there is no action for the lifecycle gate to hold back — and recording is
    /// not something that should be held back anyway, since a handover that
    /// happens during a save is exactly the one worth having a row for.
    /// </summary>
    [Fact]
    public void The_file_that_only_listens_never_calls_what_it_listens_to()
    {
        string source = AdapterSource.Read(TheOneFileThatOnlyListens);

        Assert.Contains("harmony.Patch(", source, StringComparison.Ordinal);

        foreach ((string _, int line, string text) in
                 Lines(Path.Combine(AdapterSource.Directory, TheOneFileThatOnlyListens)))
        {
            // An invocation is the method name followed by an open bracket.
            // Naming it to Harmony, as a nameof or a parameter type, is not.
            Assert.False(
                Regex.IsMatch(text, @"\.\s*ProcessHandover\w*\s*\("),
                $"{TheOneFileThatOnlyListens}:{line} calls into the handover path instead of "
                + "listening to it");
        }
    }

    /// <summary>
    /// What makes "names the watch" mean "asks the gate": one file reaches the
    /// gate, and it is the watch. A pass that built its own conditions and
    /// called the gate directly would be a second reading of the game on the
    /// same frame, and two passes could then disagree about whether a save is in
    /// progress.
    /// </summary>
    [Fact]
    public void Only_the_watch_reaches_the_gate()
    {
        var callers = new List<string>();

        foreach ((string file, int _, string text) in AdapterLines())
        {
            if (text.Contains("LifecycleGate.", StringComparison.Ordinal))
            {
                callers.Add(file);
            }
        }

        Assert.Equal(new[] { "LifecycleWatch.cs" }, callers.Distinct());
    }

    /// <summary>
    /// A file asks the gate if it holds a watch and asks it something. Naming
    /// the type in a doc comment is not asking, which is why the comments are
    /// stripped before this looks.
    /// </summary>
    private static bool AsksTheGate(string file) =>
        Lines(file).Any(entry =>
            entry.Text.Contains("LifecycleWatch", StringComparison.Ordinal)
            || entry.Text.Contains("LifecycleGate", StringComparison.Ordinal));

    /// <summary>
    /// The names the gate knows are the passes that exist. A name the adapter
    /// never uses would be a pass that quietly stopped asking.
    /// </summary>
    [Theory]
    [InlineData("LifecyclePass.Scheduling")]
    [InlineData("LifecyclePass.Counteroffers")]
    [InlineData("LifecyclePass.Handover")]
    [InlineData("LifecyclePass.Prices")]
    public void Every_pass_the_gate_knows_about_is_a_pass_that_asks_it(string pass)
    {
        Assert.Contains(pass, AdapterText());
    }

    private static string AdapterText() => AdapterSource.ReadAll();

    /// <summary>
    /// Every line of the adapter that is not a comment. The names above appear
    /// all over the doc comments — that is where the reasons live — and a
    /// reason for not doing something must not read as doing it.
    /// </summary>
    private static IEnumerable<(string File, int Line, string Text)> AdapterLines() =>
        AdapterSource.Files().SelectMany(Lines);

    private static IEnumerable<(string File, int Line, string Text)> Lines(string file)
    {
        string name = Path.GetFileName(file);
        int number = 0;

        foreach (string text in File.ReadLines(file))
        {
            number++;

            if (!IsComment(text))
            {
                yield return (name, number, text);
            }
        }
    }

    private static bool IsComment(string text)
    {
        string trimmed = text.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal);
    }
}
