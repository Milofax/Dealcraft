using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dealcraft.Core.Tests.Scenarios;

/// <summary>
/// Says which scenario a test class drives, and what that scenario is called.
/// </summary>
/// <remarks>
/// <para>
/// The heading travels with the number so that a scenario cannot be renamed
/// without the test that drives it being edited in the same change — which is
/// the ticket's own rule: <i>"A scenario edited and a test edited is one
/// change."</i>
/// </para>
/// <para>
/// A second list of scenario titles would be a second store and would drift.
/// This is not one: <see cref="AnEveningOfPlay"/> reads the file and holds the
/// attributes against it, in both directions.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class DrivesAttribute : Attribute
{
    public DrivesAttribute(int scenario, string heading)
    {
        Scenario = scenario;
        Heading = heading;
    }

    public int Scenario { get; }

    /// <summary>The scenario's heading, without its number.</summary>
    public string Heading { get; }
}

/// <summary>
/// <c>acceptance/scenarios.md</c>, read as the definition it is.
/// </summary>
/// <remarks>
/// <para>
/// <b>The assertions are the scenario's own words, and this is what makes that
/// true rather than claimed.</b> Every phrase a driver asserts goes through
/// <see cref="Says"/> first, which fails if the phrase is not in that scenario's
/// section of the file. So the tests cannot quietly outlive the sentences they
/// were written for: the owner edits a line, and the driver quoting it goes red.
/// </para>
/// <para>
/// Read as text, from the checkout, for the reason <see cref="AdapterSource"/>
/// gives about the adapter: the thing being held to is a file, and a copy of it
/// in here would be the drift this is meant to catch.
/// </para>
/// </remarks>
internal static class TheScenarioFile
{
    /// <summary>
    /// Where the file lives, relative to the mod's own root.
    /// </summary>
    /// <remarks>
    /// It used to be the project notes in the coordination
    /// repo, which meant these thirty drivers could only run there: the public
    /// repository is built from <c>src/Dealcraft</c> alone, so every one of them
    /// failed from a clean copy and <c>bin/publish-dealcraft</c> refused to ship.
    /// The scenarios are the mod's own acceptance criteria and are written for a
    /// player, so they belong with the mod and go out with it.
    /// </remarks>
    public const string Path = "acceptance/scenarios.md";

    private static readonly Lazy<IReadOnlyDictionary<int, string>> Sections = new(Split);

    /// <summary>Every numbered scenario in the file, by its number.</summary>
    public static IReadOnlyDictionary<int, string> All => Sections.Value;

    /// <summary>The heading of one scenario, without its number.</summary>
    public static string Heading(int scenario) =>
        Section(scenario).Split('\n')[0].Trim();

    /// <summary>
    /// One scenario's text, heading included.
    /// </summary>
    public static string Section(int scenario) =>
        All.TryGetValue(scenario, out string? text)
            ? text
            : throw new KeyNotFoundException(
                $"{Path} has no scenario {scenario}. The file holds "
                + $"{string.Join(", ", All.Keys)}.");

    /// <summary>
    /// A phrase the scenario uses, handed back so a driver can assert against it.
    /// Throws when the scenario no longer says it.
    /// </summary>
    /// <remarks>
    /// Whitespace-insensitive, because the file is wrapped at eighty columns and
    /// a sentence the owner did not touch can still change which line it breaks
    /// on. Nothing else is relaxed: the words and their order are the scenario's.
    /// </remarks>
    public static string Says(int scenario, string phrase)
    {
        if (!Flattened(Section(scenario)).Contains(Flattened(phrase), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"scenario {scenario} of {Path} no longer says \"{phrase}\". "
                + "The scenario and the test that drives it are one change: either "
                + "the quotation here is stale, or the scenario moved and this "
                + "driver has to move with it.");
        }

        return phrase;
    }

    /// <summary>
    /// Every scenario the file holds, as `1.` through `8.` headings.
    /// </summary>
    private static IReadOnlyDictionary<int, string> Split()
    {
        string text = File.ReadAllText(Find());
        var sections = new Dictionary<int, string>();

        MatchCollection headings = Regex.Matches(
            text, @"^## (?<number>\d+)\. (?<heading>.+)$", RegexOptions.Multiline);

        for (int i = 0; i < headings.Count; i++)
        {
            int from = headings[i].Index;
            int to = i + 1 < headings.Count ? headings[i + 1].Index : text.Length;

            sections[int.Parse(headings[i].Groups["number"].Value)] =
                headings[i].Groups["heading"].Value + "\n" + text[from..to];
        }

        return sections;
    }

    /// <summary>
    /// Found by walking up from the test assembly, the way
    /// <see cref="AdapterSource"/> finds the adapter: a path written down would
    /// go stale the first time either moved, and a scenario file that cannot be
    /// found has to say so rather than let every driver pass vacuously.
    /// </summary>
    private static string Find()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = System.IO.Path.Combine(
                directory.FullName, Path.Replace('/', System.IO.Path.DirectorySeparatorChar));

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"{Path} was not found above {AppContext.BaseDirectory}. These drivers hold the "
            + "mod against the scenarios; without the scenarios they hold it against nothing.");
    }

    private static string Flattened(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
