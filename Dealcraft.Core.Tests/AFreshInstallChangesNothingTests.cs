using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Installing the mod changes nothing until the owner changes something. That is
/// the first promise <c>app.md</c> makes, and until this ticket it was not true.
/// </summary>
/// <remarks>
/// <para>
/// <c>DealSchedulingFeature</c> defaulted each window to
/// <c>window == DealWindow.LateNight</c> — Late Night shipped ticked, because
/// that is the window the original mod applied to every deal. It was harmless
/// only while <c>AutoScheduleDeals</c> sat off above it, and this page deletes
/// that switch: four ticked boxes already say which windows are allowed.
/// </para>
/// <para>
/// So the defaults are the fix, and this is the test that fails if a later
/// worker restores the one that looked reasonable.
/// </para>
/// </remarks>
public class AFreshInstallChangesNothingTests
{
    /// <summary>
    /// Read out of the adapter, because the defaults live where the
    /// MelonPreferences entries are created and no test here may follow them
    /// into the game.
    /// </summary>
    [Fact]
    public void All_four_windows_are_declared_off()
    {
        string source = AdapterSource.Read(System.IO.Path.Combine("Features", "DealSchedulingFeature.cs"));

        Match declaration = Regex.Match(
            source,
            @"CreateEntry\(\s*DealWindowCatalog\.KeyOf\(window\)\s*,\s*(?<default>[A-Za-z0-9_.= ]+?)\s*,");

        Assert.True(declaration.Success, "the four window entries are not declared from one loop any more");
        Assert.Equal("false", declaration.Groups["default"].Value);
    }

    /// <summary>
    /// And nothing above them can turn them all on at once, because there is no
    /// switch above them. Four ticked boxes already say which windows the host
    /// allows; a parent switch could only contradict them.
    /// </summary>
    [Fact]
    public void There_is_no_switch_above_the_four_windows()
    {
        Assert.DoesNotContain("AutoScheduleDeals", AdapterSource.ReadAll(), StringComparison.Ordinal);

        Assert.DoesNotContain(
            typeof(AdvisorSettings).GetProperties(),
            property => property.Name == "AutoScheduleDeals");
    }

    /// <summary>
    /// What a fresh install therefore draws: four windows, none of them ticked,
    /// and no fifth row.
    /// </summary>
    [Fact]
    public void The_page_of_a_fresh_install_shows_four_windows_and_none_of_them_ticked()
    {
        AutomationBlock schedule = AutomationForm
            .Build(AutomationCatalog.Describe(new AdvisorSettings())
                .Concat(DealWindowCatalog.Describe(new DealWindowSet(), Array.Empty<DealWindowHours>()))
                .Concat(PriceMaintenanceCatalog.Describe(maintaining: false))
                .ToArray(), ServerAuthority.Held)
            .Blocks.Single(block => block.Title == "ACCEPTED SCHEDULE");

        Assert.Equal(4, schedule.Rows.Count);
        Assert.All(schedule.Rows, row => Assert.False(row.Chosen));
    }

    /// <summary>
    /// And scheduling is inert with no window allowed, which is what "no switch
    /// above them" costs and what it has to buy back.
    /// </summary>
    [Fact]
    public void With_no_window_allowed_nothing_is_scheduled()
    {
        Assert.True(new DealWindowSet().AllowsNothing);

        ScheduleDecision decision = DealScheduler.Consider(
            new PendingOffer("npc-jessi#4:2330", "Jessi Waters", hasOfferedContract: true, alreadyOnADeal: false),
            ServerAuthority.Held,
            automationOn: false);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
    }

    /// <summary>
    /// The keys the page stopped drawing left the file with it. A setting the
    /// interface cannot account for is a defect, not a power-user feature, and a
    /// preferences file that still carries any of these loads without being read,
    /// rewritten or warned about.
    /// </summary>
    [Fact]
    public void Every_setting_the_page_stopped_drawing_left_the_preferences_file()
    {
        string[] gone =
        {
            "AutoScheduleDeals", "MaximumPricePerUnit", "MinimumCounterGain",
            "MaxProbesPerNegotiation", "CrowdedWindowTolerance", "ExcludedCustomerNames",
        };

        string adapter = AdapterSource.ReadAll();

        Assert.DoesNotContain(gone, key => adapter.Contains(key, StringComparison.Ordinal));
    }
}
