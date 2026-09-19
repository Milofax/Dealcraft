using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// A preferences file written by an older install carries keys this build no
/// longer declares. It loads without a warning, and none of them is read or
/// reset.
/// </summary>
/// <remarks>
/// <para>
/// This used to open by counting them, and the count was wrong — it said fifteen
/// over a list of eleven. The number is not written down now: the list below is
/// the record, and a number beside a list is a second copy of it that nothing
/// checks.
/// </para>
/// <para>
/// <b>Read out of MelonLoader 0.7.3 rather than assumed</b>, because the whole
/// promise turns on what saving does to a key nobody declared.
/// <c>MelonLoader.Preferences.IO.File.Load</c> parses the whole file into one
/// <c>TomlDocument</c>; <c>MelonPreferences.LoadFileAndRefreshCategories</c>
/// then walks the <em>declared</em> entries and fills each from that document,
/// so a key no category declares is never visited — not read, not typed, not
/// complained about. <c>MelonPreferences_Category.SaveToFile</c> inserts each
/// declared entry back into the same document and writes the document whole, and
/// the only calls that drop anything from it are <c>DeleteEntry</c>,
/// <c>RenameEntry</c> and <c>RemoveCategoryFromFile</c>.
/// </para>
/// <para>
/// So the guard this side of the seam is that Dealcraft calls none of the three.
/// The behaviour is MelonLoader's; what is ours is not asking it to forget
/// anything.
/// </para>
/// </remarks>
public class AnOldPreferencesFileStillLoadsTests
{
    /// <summary>
    /// Every key this project has ever declared and since deleted. Written down
    /// so that a worker who meets one of them in a player's file can see it was
    /// left there on purpose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list failed its own stated scope for a while: four keys from before
    /// the rebuild were missing, and all four are in the owner's live
    /// <c>MelonPreferences.cfg</c> right now. They are the last four entries
    /// below. Each was checked back to the commit that declared it and the commit
    /// that took it away.
    /// </para>
    /// <para>
    /// <c>PauseBacklogWhileSaving</c> is on the list and is <em>not</em> in the
    /// owner's file, which is not a defect: it was declared by <c>6066d74</c> and
    /// deleted by <c>accab83</c> in the same night, so no build he ran ever wrote
    /// it. The scope here is what this project declared, not what one install
    /// happens to hold.
    /// </para>
    /// </remarks>
    private static readonly string[] Deleted =
    {
        // The two tabs and the overlay they were reached from.
        "OverlayKey",

        // The save guard, which is not a decision a player should be handed.
        "PauseAutomationWhileSaving",

        // The backlog feature, whole.
        "AutoWorkOfferBacklog", "BacklogOffersPerTick", "PauseBacklogWhileSaving",

        // The switch above the four windows, and the warning that refused
        // nothing.
        "AutoScheduleDeals", "CrowdedWindowTolerance",

        // File-only settings the page never showed.
        "MaximumPricePerUnit", "MinimumCounterGain", "MaxProbesPerNegotiation",

        // Set on the customer, by giving them to a dealer — with the cost of
        // that recorded in app.md and docs/counteroffer-truth.md, because the
        // dealer takes the customer rather than leaving them alone.
        "ExcludedCustomerNames",

        // Older than the rebuild, and all four still in the owner's file.
        // The price floor that stopped being a switch (d871a7c), and the goal
        // selector he threw out (6d74d16).
        "MinimumPricePerUnit", "MinimumFromListedPrice",
        "TargetQuantity", "RaiseQuantityToMaxSpend",
    };

    /// <summary>
    /// Asked of the keys the adapter really declares rather than of the text
    /// <c>"key"</c> appearing in it, so that a deleted setting coming back under
    /// a constant is caught too.
    /// </summary>
    [Fact]
    public void Not_one_deleted_key_is_declared_anywhere_in_the_adapter()
    {
        Assert.Empty(PreferencesFile.EntryNames().Intersect(Deleted, StringComparer.Ordinal));
    }

    /// <summary>
    /// And nothing asks MelonLoader to forget them. A file that still carries
    /// them is a file the player may go back to an older build with, and a mod
    /// that tidied up would have taken their settings with it.
    /// </summary>
    [Fact]
    public void Nothing_deletes_renames_or_removes_anything_from_the_preferences_file()
    {
        string[] forgetting = { "DeleteEntry", "RenameEntry", "RemoveCategoryFromFile" };

        var found = new List<string>();

        foreach (string call in forgetting)
        {
            if (AdapterSource.ReadAll().Contains(call, StringComparison.Ordinal))
            {
                found.Add(call);
            }
        }

        Assert.Empty(found);
    }

    /// <summary>
    /// The other half, so that the two lists cannot quietly overlap: no key the
    /// page draws is on the deleted list.
    /// </summary>
    [Fact]
    public void No_key_the_page_draws_is_one_of_the_deleted_ones()
    {
        IEnumerable<string> drawn = AutomationForm
            .Build(AutomationCatalog.Describe(new AdvisorSettings())
                .Concat(DealWindowCatalog.Describe(new DealWindowSet(), Array.Empty<DealWindowHours>()))
                .Concat(PriceMaintenanceCatalog.Describe(maintaining: false))
                .ToArray(), ServerAuthority.Held)
            .Claimed
            .Concat(AutomationForm.FileOnly);

        Assert.Empty(drawn.Intersect(Deleted, StringComparer.Ordinal));
    }
}
