using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The app is one page. There is no Customers tab, no Products tab and no strip
/// to choose between them, and none of the three comes back.
/// </summary>
/// <remarks>
/// <para>
/// The owner was shown the interface whole for the first time and deleted most
/// of it in one sitting: <i>"Reiter 1 und 2 brauche ich erst einmal gar nicht.
/// Aber 1 und 2 werde ich mir niemals angucken."</i> The game has a customer
/// list and a product list of its own, and a mod that lists them again is a
/// second place to read the same thing.
/// </para>
/// <para>
/// A deletion stays deleted only if something fails when it comes back, which is
/// <see cref="OnePlaceDrawsTests"/>'s pattern and the reason this is a test
/// rather than a note. The renderers are named by file and by type, because a
/// worker who thinks the page looks unfinished would restore a file and a worker
/// reading a stack trace would restore a class — and the two are not the same
/// name here: the master list's bar recipe lived in <c>DetailStrip</c>, inside a
/// file called <c>Gauges.cs</c>.
/// </para>
/// <para>
/// Checked against the adapter's source because every one of them reached into
/// the running game, and no test here may reference an Il2Cpp type. See
/// <see cref="AdapterSource"/>.
/// </para>
/// </remarks>
public class TheTwoTabsAreGoneTests
{
    /// <summary>
    /// The renderers the two tabs and the strip were drawn by, and the readers
    /// that existed to feed them, by file name.
    /// </summary>
    private static readonly string[] GoneFiles =
    {
        // The master list on the left and the detail panel on the right.
        "AppShellView",
        "DetailView",
        "RowList",

        // The bars down the detail panel, and the palette that tinted them.
        "Gauges",
        "GamePalette",

        // The strip across the top: what the game lent, and what wore it.
        "TabStrip",
        "TabStripView",

        // The readers beneath the two tabs. Everything they read is in the game
        // already, on screens the player is standing in front of.
        "AppContentReader",
        "CustomerValuationReader",
    };

    /// <summary>
    /// The same deletion by type, because a file name and a class name are not
    /// the same thing in this tree and restoring either is restoring the tab.
    /// </summary>
    private static readonly string[] GoneTypes =
    {
        "AppShellView",
        "DetailView",
        "DetailLine",
        "DetailBlock",
        "DetailStrip",
        "RowList",
        "GamePalette",
        "TabStrip",
        "TabStripView",
        "TabWidgets",
        "AppContentReader",
        "CustomerValuationReader",
    };

    [Fact]
    public void No_file_of_the_two_tabs_or_the_strip_is_in_the_adapter()
    {
        IEnumerable<string> files = AdapterSource.Files()
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty);

        Assert.Empty(files.Intersect(GoneFiles, StringComparer.Ordinal));
    }

    /// <summary>
    /// And nothing left behind names them. A field, a call or a commented-out
    /// line would be the deletion half-made.
    /// </summary>
    /// <remarks>
    /// Doc comments are passed over, because the comment that explains a
    /// deletion necessarily says what was deleted — <c>DonorControls.Bar</c>
    /// records where the scrollbar-clone recipe went and why, which is the point
    /// of writing it down.
    /// </remarks>
    [Fact]
    public void Nothing_in_the_adapter_still_names_them()
    {
        var named = new List<string>();

        foreach (string name in GoneTypes)
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
    /// The model the two tabs were drawn from is gone as well, so there is
    /// nothing left for a renderer to be restored against: no tab enum, no
    /// shell, no master-list row, no detail panel, and no insight to fill one.
    /// </summary>
    /// <remarks>
    /// By reflection over the core assembly rather than over its source, because
    /// this is the half of the deletion a test can ask directly.
    /// </remarks>
    [Fact]
    public void The_model_the_two_tabs_were_drawn_from_is_gone()
    {
        string[] gone =
        {
            "AppTab", "AppTabs", "AppShell", "AppContent", "AppEntry", "AppSection",
            "AppDetail", "DetailRow", "DetailSection", "DetailGauge", "DetailLayout",
            "CustomerInsights", "ProductInsights", "CustomerValuation",
            "StockAgainstDemand", "IStockAgainstDemand", "StripPlacement", "GaugeLayout",
            "ListTop", "ScrollTop", "NameFit",
        };

        var found = gone
            .Where(name => typeof(AutomationForm).Assembly.GetType($"Dealcraft.Core.{name}") is not null)
            .ToList();

        Assert.Empty(found);
    }

    /// <summary>
    /// What the page is instead: the settings form, and nothing beside it. The
    /// app's own view builds one <see cref="AutomationForm"/> and draws it, so
    /// there is no second screen for a strip to switch to.
    /// </summary>
    [Fact]
    public void The_app_draws_one_page_and_it_is_the_settings_form()
    {
        string page = AdapterSource.Read(Path.Combine("Phone", "AppPageView.cs"));

        Assert.Contains("AutomationForm.Build(", page, StringComparison.Ordinal);
        Assert.Contains("new AutomationView(", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the README does not still sell them. It is the release-facing
    /// document — the one a friend installing the mod reads first and last — and
    /// it promised the customer and product numbers in the app, and a best price
    /// "shown", for the whole of the night that deleted both.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Held by phrases rather than by a whole paragraph, because the point is
    /// that a sentence describing a screen that is gone must fail something. The
    /// phrases are the ones that were there; a rewrite that reintroduces the
    /// claim in other words is beyond what a test can catch, and the first
    /// section of the README now says plainly what the app does not show.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_shipped_readme_does_not_advertise_the_two_tabs()
    {
        string readme = File.ReadAllText(
            Path.Combine(Directory.GetParent(AdapterSource.Directory)!.FullName, "README.md"));

        string[] gone =
        {
            "Shows the customer and product numbers",
            "customers, shows it",
            "shows you the numbers it used to decide",
        };

        Assert.DoesNotContain(gone, claim => readme.Contains(claim, StringComparison.Ordinal));
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

                if (text.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return (name, number, text);
            }
        }
    }
}
