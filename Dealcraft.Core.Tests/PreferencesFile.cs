using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Dealcraft.Core;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Every key <c>MelonPreferences.cfg</c> would hold after this build declared
/// its settings, read out of the adapter's own source.
/// </summary>
/// <remarks>
/// <para>
/// The file half of "every setting is in the file and in the app" lives in the
/// Il2Cpp adapter, where no test may follow it. So the far side is read as text
/// — see <see cref="AdapterSource"/> for why that is the seam rather than a
/// shortcut — and this is the one place that reads it, because two guards that
/// scrape the same thing differently are two guards with different blind spots.
/// </para>
/// <para>
/// <b>It fails on what it cannot read, and that is the whole design.</b> The
/// scraper this replaces matched three shapes of <c>CreateEntry</c> call and
/// silently returned nothing for a fourth, so a key named through a dotted
/// constant — <c>PriceMaintenanceCatalog.Key</c>, which ships — was invisible to
/// every guard built on it. A guard that yields nothing when it does not
/// understand something is not a guard; it is a test that passes. Here an
/// argument that resolves to no name throws, and the message names the call.
/// </para>
/// </remarks>
internal static class PreferencesFile
{
    /// <summary>
    /// Every name passed to <c>category.CreateEntry</c> anywhere in the adapter,
    /// in ordinal order. Matched on the call rather than on a list written down
    /// twice, which would be the very duplication the guards exist to catch.
    /// </summary>
    public static IReadOnlyList<string> EntryNames() => NamesIn(AdapterSource.ReadAll());

    /// <summary>
    /// The same reading, over source handed in rather than read off disk. This
    /// exists so that <see cref="PreferencesFileTests"/> can watch the guard fail
    /// on a key it cannot read — a guard nobody has seen fail is the thing this
    /// repository keeps learning about.
    /// </summary>
    public static IReadOnlyList<string> NamesIn(string text)
    {
        string source = WithoutComments(text);
        var names = new List<string>();

        foreach (Match call in Regex.Matches(source, @"\bCreateEntry\s*\("))
        {
            names.AddRange(Resolve(FirstArgument(source, call.Index + call.Length), source));
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// The name a call gives its entry, however it spells it. Four shapes are
    /// understood; anything else stops the run rather than disappearing.
    /// </summary>
    private static IEnumerable<string> Resolve(string argument, string source)
    {
        Match literal = Regex.Match(argument, @"^""(?<name>[^""]+)""$");
        if (literal.Success)
        {
            return new[] { literal.Groups["name"].Value };
        }

        // The four window entries are generated from the same list the app's
        // rows are, so they are resolved the only way they can be: by asking the
        // catalogue that generates them.
        if (Regex.IsMatch(argument, @"^DealWindowCatalog\.KeyOf\("))
        {
            return DealWindowSet.All.Select(DealWindowCatalog.KeyOf);
        }

        Match named = Regex.Match(
            argument,
            @"^(?:(?<type>[A-Za-z_][A-Za-z0-9_]*)\.)?(?<member>[A-Za-z_][A-Za-z0-9_]*)$");
        if (named.Success)
        {
            string type = named.Groups["type"].Value;
            string member = named.Groups["member"].Value;
            string? constant = InTheCore(type, member) ?? InTheAdapter(member, source);

            if (constant is not null)
            {
                return new[] { constant };
            }
        }

        throw new InvalidOperationException(
            $"CreateEntry({argument}, …) names its preferences key in a way this guard cannot "
            + "read, so the key would be invisible to every check that the file and the app "
            + "agree. Teach PreferencesFile.Resolve the shape, or name the key with a literal "
            + "or a public string constant.");
    }

    /// <summary>
    /// A key published as a constant on a catalogue, which is how a feature lets
    /// its own tests name it. Those catalogues are in <c>Dealcraft.Core</c> and
    /// this assembly references it, so the value is read rather than re-parsed.
    /// </summary>
    private static string? InTheCore(string type, string member)
    {
        if (type.Length == 0)
        {
            return null;
        }

        Type? declaring = typeof(AutomationSetting).Assembly
            .GetTypes()
            .FirstOrDefault(candidate => candidate.Name == type);

        FieldInfo? field = declaring?.GetField(member, BindingFlags.Public | BindingFlags.Static);

        return field is { IsLiteral: true } ? field.GetRawConstantValue() as string : null;
    }

    /// <summary>
    /// A key declared as a constant in the adapter itself, where reflection
    /// cannot reach. Two declarations of the same name would make the answer a
    /// coin toss, so two is an error rather than a first match.
    /// </summary>
    private static string? InTheAdapter(string member, string source)
    {
        string[] found = Regex
            .Matches(source, @"const\s+string\s+" + Regex.Escape(member) + @"\s*=\s*""(?<value>[^""]+)""")
            .Select(match => match.Groups["value"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (found.Length > 1)
        {
            throw new InvalidOperationException(
                $"The adapter declares `const string {member}` {found.Length} times with different "
                + $"values ({string.Join(", ", found)}), so which one a CreateEntry call means "
                + "cannot be read from the source. Rename one.");
        }

        return found.Length == 1 ? found[0] : null;
    }

    /// <summary>
    /// The text between an open bracket and the first comma outside brackets and
    /// strings — the call's first argument, whatever it is made of.
    /// </summary>
    private static string FirstArgument(string source, int start)
    {
        int depth = 0;

        for (int index = start; index < source.Length; index++)
        {
            char character = source[index];

            if (character is '"')
            {
                index = EndOfString(source, index);
                continue;
            }

            if (character is '(' or '[' or '{')
            {
                depth++;
            }
            else if (character is ')' or ']' or '}')
            {
                if (character is ')' && depth == 0)
                {
                    return source[start..index].Trim();
                }

                depth--;
            }
            else if (character is ',' && depth == 0)
            {
                return source[start..index].Trim();
            }
        }

        throw new InvalidOperationException(
            $"A CreateEntry call at offset {start} of the adapter's source is never closed.");
    }

    private static int EndOfString(string source, int quote)
    {
        for (int index = quote + 1; index < source.Length; index++)
        {
            if (source[index] == '\\')
            {
                index++;
            }
            else if (source[index] == '"')
            {
                return index;
            }
        }

        return source.Length;
    }

    /// <summary>
    /// The source with its comments blanked and its strings kept. A doc comment
    /// that mentions <c>CreateEntry(…)</c> is prose, and the guard above stops
    /// the run on anything it cannot resolve — so prose has to be removed before
    /// it is read, not tolerated afterwards.
    /// </summary>
    private static string WithoutComments(string source) => Regex.Replace(
        source,
        @"(?<keep>@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*"")|(?<comment>//[^\n]*|/\*.*?\*/)",
        match => match.Groups["comment"].Success ? " " : match.Value,
        RegexOptions.Singleline);
}
