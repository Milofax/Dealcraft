using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Dealcraft draws in exactly one place: its own app.
/// </summary>
/// <remarks>
/// <para>
/// The owner's instruction was to encapsulate it and go on stealing the look:
/// <i>"Ich würde das jetzt erstmal alles in unserer App kapseln, aber natürlich
/// aus der Kontakte App etc. klauen, dass es im Style und vom Aussehen gleich
/// aussieht."</i> Both halves are checked here, because they pull in opposite
/// directions and a build could satisfy either one alone: the app must still be
/// built out of the game's own widgets, and none of Dealcraft's rows may be
/// attached into a panel the game owns.
/// </para>
/// <para>
/// Checked against the adapter's source because the rule is about code that
/// reaches into the running game, and no test here may reference an Il2Cpp type.
/// See <see cref="AdapterSource"/>.
/// </para>
/// </remarks>
public class OnePlaceDrawsTests
{
    /// <summary>
    /// The two classes that rendered Dealcraft's blocks into the game's own
    /// panels, and the container they shared. Deleted, not disabled — a disabled
    /// renderer is one setting away from being a tenant again.
    /// </summary>
    /// <remarks>
    /// The blocks they drew are gone too, with the two tabs that showed them.
    /// <see cref="TheTwoTabsAreGoneTests"/> holds that deletion; this one still
    /// holds the older rule, which is about <em>where</em> Dealcraft may draw.
    /// </remarks>
    private static readonly string[] Gone =
    {
        "ContactsInsights",
        "ProductInsightsPanel",
        "InsightSections",
    };

    [Fact]
    public void The_renderers_that_lived_in_the_games_panels_are_gone()
    {
        IEnumerable<string> files = AdapterSource.Files()
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty);

        Assert.Empty(files.Intersect(Gone, StringComparer.Ordinal));
    }

    /// <summary>
    /// And nothing left behind names them. A field, a feature registration or a
    /// commented-out line would be the move half-made.
    /// </summary>
    [Fact]
    public void Nothing_in_the_adapter_still_names_them()
    {
        var named = new List<string>();

        foreach (string name in Gone)
        {
            foreach ((string file, int line, string text) in Lines())
            {
                if (text.Contains(name, StringComparison.Ordinal))
                {
                    named.Add($"{file}:{line} names {name}");
                }
            }
        }

        Assert.Empty(named);
    }

    /// <summary>
    /// The theft, still going. The app's widgets come from the Product Manager's
    /// own detail panel and its palette from the Contacts panel, which is why it
    /// looks as though the game drew it. This is the half that must <em>not</em>
    /// change, and the file that does it is the app's layout reader.
    /// </summary>
    [Fact]
    public void The_app_still_takes_its_widgets_from_the_games_own_panels()
    {
        string layout = AdapterSource.Read(Path.Combine("Phone", "AppLayout.cs"));

        Assert.Contains("ContactsDetailPanel", layout, StringComparison.Ordinal);
        Assert.Contains("DetailStyle.Of(", layout, StringComparison.Ordinal);
        Assert.Contains("AddictionSlider", layout, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reading a panel for its look is allowed; asking it for a container to
    /// hang rows in is not. <c>FindObjectOfType&lt;ContactsDetailPanel&gt;</c> is
    /// how the palette is fetched, and it is the one mention there should be.
    /// </summary>
    [Fact]
    public void The_contacts_panel_is_read_for_its_palette_and_nowhere_else()
    {
        var files = new List<string>();

        foreach ((string file, int _, string text) in Lines())
        {
            if (text.Contains("ContactsDetailPanel", StringComparison.Ordinal))
            {
                files.Add(file);
            }
        }

        Assert.Equal(new[] { "AppLayout.cs" }, files.Distinct());
    }

    private static IEnumerable<(string File, int Line, string Text)> Lines()
    {
        foreach (string path in AdapterSource.Files())
        {
            string name = Path.GetFileName(path);
            int number = 0;
            bool inBlockComment = false;

            foreach (string raw in File.ReadLines(path))
            {
                number++;
                string text = raw.Trim();

                // Naming a deleted class in a doc comment is history, not a
                // dependency on it — and the comments that explain the move
                // necessarily say what moved.
                if (inBlockComment)
                {
                    if (text.Contains("*/", StringComparison.Ordinal))
                    {
                        inBlockComment = false;
                    }

                    continue;
                }

                if (text.StartsWith("/*", StringComparison.Ordinal))
                {
                    inBlockComment = !text.Contains("*/", StringComparison.Ordinal);
                    continue;
                }

                if (text.StartsWith("//", StringComparison.Ordinal)
                    || text.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return (name, number, text);
            }
        }
    }
}
