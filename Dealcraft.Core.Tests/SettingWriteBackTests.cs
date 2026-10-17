using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The other half of the spec's "both ways" rule, checked rather than promised:
/// "a change made there writes straight back and flushes to
/// <c>UserData/MelonPreferences.cfg</c> immediately".
///
/// <see cref="AutomationCatalogTests"/> already proves the file's entries and
/// the app's rows are the same list. This proves the way back: every entry the
/// adapter declares is also one the adapter can write. A setting declared and
/// never written would give a row that says "select this row again to set it to
/// On" and does nothing when a player does.
///
/// The adapter is read as text for the reason <see cref="AdapterSource"/> gives:
/// this is about code on the far side of the seam, which no test here may call.
/// </summary>
public class SettingWriteBackTests
{
    /// <summary>
    /// Whatever <c>category.CreateEntry</c> was assigned to — a field, or an
    /// entry in a dictionary of them.
    /// </summary>
    private static readonly Regex Declared = new(
        @"(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*(\[[^\]]*\])?\s*=\s*category\.CreateEntry");

    /// <remarks>
    /// A file that owns a <see cref="FileOnlySettings"/> entry is exempt, and
    /// exempt for the opposite reason to the one this guard is about: its
    /// setting has no row in the app at all, so there is nothing a player could
    /// press. <c>AutomationCatalogTests</c> holds it to that in both directions.
    /// </remarks>
    [Fact]
    public void Every_file_that_declares_settings_can_also_write_them()
    {
        foreach (string file in AdapterSource.Files())
        {
            if (FileOnlySettings.Files.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            if (!source.Contains("category.CreateEntry", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                ChangeBody(source) is not null,
                $"{Path.GetFileName(file)} declares MelonPreferences entries but has no Change method, "
                + "so a player pressing one of its rows in the app would change nothing");
        }
    }

    /// <summary>
    /// Not just that a file can write something, but that it writes back every
    /// entry it declared. The whole point of one owner per setting is that the
    /// owner takes all of them.
    /// </summary>
    [Fact]
    public void Every_declared_setting_is_named_where_its_owner_writes_it_back()
    {
        foreach (string file in AdapterSource.Files())
        {
            if (FileOnlySettings.Files.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            string? change = ChangeBody(source);
            if (change is null)
            {
                continue;
            }

            foreach (string target in Targets(source))
            {
                Assert.True(
                    Regex.IsMatch(change, @"\b" + Regex.Escape(target) + @"\b"),
                    $"{Path.GetFileName(file)} declares {target} but never writes it back, so that "
                    + "setting can be read in the app and not changed there");
            }
        }
    }

    /// <summary>
    /// A change is worth nothing if it only reaches memory: the spec asks for a
    /// flush "immediately, so a crash cannot lose it". One place saves the
    /// category, which is the same place that owns it.
    /// </summary>
    [Fact]
    public void The_one_owner_of_the_category_flushes_it_when_a_setting_changes()
    {
        string? change = ChangeBody(AdapterSource.Read("ModSettings.cs"));

        Assert.NotNull(change);
        Assert.Contains("SaveToFile", change!, StringComparison.Ordinal);
    }

    private static IEnumerable<string> Targets(string source) => Declared
        .Matches(source)
        .Select(match => match.Groups["target"].Value)
        .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// The body of this file's <c>Change</c> method, or null when it has none.
    /// Found by matching braces from the signature rather than by a line count,
    /// so reformatting the file does not quietly turn the guard off. An
    /// expression body — which is what a feature with one setting writes — ends
    /// at its semicolon instead.
    /// </summary>
    private static string? ChangeBody(string source)
    {
        Match signature = Regex.Match(source, @"bool Change\(");
        if (!signature.Success)
        {
            return null;
        }

        int open = source.IndexOf('{', signature.Index);
        int arrow = source.IndexOf("=>", signature.Index, StringComparison.Ordinal);

        if (arrow >= 0 && (open < 0 || arrow < open))
        {
            int end = source.IndexOf(';', arrow);
            return end < 0 ? null : source.Substring(arrow, end - arrow);
        }

        if (open < 0)
        {
            return null;
        }

        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}' && --depth == 0)
            {
                return source.Substring(open, i - open + 1);
            }
        }

        return null;
    }
}
