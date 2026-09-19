using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// There is no backlog pass. Not the switch — the whole feature.
/// </summary>
/// <remarks>
/// <para>
/// The control was <c>Also accept the offers that were waiting</c>, and the
/// owner's verdict on it was <i>"Was ist denn das für ein Bullshit?"</i> —
/// followed by <i>"ich weiß gar nicht, was das ist"</i>. It was the feature's
/// only switch, so deleting the control would have left nine files and about a
/// thousand lines that nothing could ever reach again.
/// </para>
/// <para>
/// <c>CLAUDE.md</c> calls a key the interface cannot account for a defect. A
/// whole feature the interface cannot account for is that defect in large, so
/// the faithful reading of his words is the feature and not the switch. If the
/// intent was ever to keep the behaviour and lose the choice, the pass comes
/// back running unconditionally under scheduling — but it comes back on a
/// decision, which is why this is a test.
/// </para>
/// </remarks>
public class TheBacklogIsGoneTests
{
    /// <summary>The model the pass was built out of, by type.</summary>
    [Fact]
    public void Nothing_in_the_core_is_a_backlog()
    {
        string[] gone =
        {
            "OfferBacklog", "BacklogOffer", "BacklogOrder", "BacklogReport",
            "BacklogBudget", "BacklogCatalog", "BacklogPhase",
        };

        Assert.DoesNotContain(
            gone, name => typeof(AutomationForm).Assembly.GetType($"Dealcraft.Core.{name}") is not null);
    }

    /// <summary>
    /// And the pass, its feature and its configuration, by file — because a
    /// worker reading a stack trace restores a class and a worker who thinks the
    /// page looks thin restores a file.
    /// </summary>
    [Fact]
    public void No_file_of_the_backlog_pass_is_in_the_adapter()
    {
        string[] gone = { "OfferBacklogPass", "OfferBacklogFeature", "BacklogConfiguration" };

        IEnumerable<string> files = AdapterSource.Files()
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty);

        Assert.Empty(files.Intersect(gone, StringComparer.Ordinal));
    }

    /// <summary>
    /// The seams scheduling kept for it go too. <c>StandBy</c> held the sweep
    /// while the pass worked the offers oldest-first, and <c>Work</c> was the
    /// door it handed them through; with no caller, both are a way back in.
    /// </summary>
    [Fact]
    public void Scheduling_has_no_door_left_for_a_backlog_to_hand_offers_through()
    {
        string scheduling = AdapterSource.Read("DealSchedulingLoop.cs");

        Assert.DoesNotContain("StandBy", scheduling, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool Work(", scheduling, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the preferences file holds neither of its two entries. A save the
    /// owner has played is a file with both lines in it; they are not read, not
    /// rewritten and not warned about.
    /// </summary>
    [Fact]
    public void Neither_of_its_two_entries_is_in_the_preferences_file()
    {
        string adapter = AdapterSource.ReadAll();

        Assert.DoesNotContain("AutoWorkOfferBacklog", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("BacklogOffersPerTick", adapter, StringComparison.Ordinal);
    }
}
