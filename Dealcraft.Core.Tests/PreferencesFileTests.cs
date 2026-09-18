using System;
using System.Linq;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The guard that holds the preferences file against the page, held against
/// itself.
/// </summary>
/// <remarks>
/// <para>
/// Every other test in this assembly trusts <see cref="PreferencesFile"/> to say
/// which keys the adapter declares. It was trusted before, and it was wrong: the
/// scraper understood three spellings of a <c>CreateEntry</c> call and returned
/// nothing at all for the fourth, so a shipping key was invisible on both sides
/// of the comparison and the test passed by subtracting it from each. Nobody had
/// ever seen it fail.
/// </para>
/// <para>
/// So these are the cases where it must fail, and they run on source written
/// here rather than on the adapter, because the adapter is not allowed to hold a
/// key that breaks the build just to prove a point.
/// </para>
/// </remarks>
public class PreferencesFileTests
{
    /// <summary>
    /// The shape that got past it. A dotted constant is neither a literal nor a
    /// bare identifier, and the old constant pattern required a comma straight
    /// after the name.
    /// </summary>
    [Fact]
    public void A_key_named_through_a_dotted_constant_is_read()
    {
        Assert.Equal(
            new[] { "MaintainListedPrices" },
            PreferencesFile.NamesIn("category.CreateEntry(PriceMaintenanceCatalog.Key, false);"));
    }

    /// <summary>And the key that was in that blind spot is really out of it.</summary>
    [Fact]
    public void The_shipping_key_that_was_invisible_is_visible()
    {
        Assert.Contains("MaintainListedPrices", PreferencesFile.EntryNames());
    }

    [Fact]
    public void A_key_named_with_a_literal_is_read()
    {
        Assert.Equal(
            new[] { "AutoHandover" },
            PreferencesFile.NamesIn("category.CreateEntry(\"AutoHandover\", true, description: \"x\");"));
    }

    /// <summary>
    /// The adapter's own constants, where reflection cannot follow. Resolved out
    /// of the same source the call is in.
    /// </summary>
    [Fact]
    public void A_key_named_through_a_constant_in_the_adapter_is_read()
    {
        Assert.Equal(
            new[] { "HandoverLedger" },
            PreferencesFile.NamesIn(
                "public const string Key = \"HandoverLedger\";\ncategory.CreateEntry(Key, false);"));
    }

    /// <summary>
    /// The four window entries are generated from the catalogue's own list, so
    /// the guard asks the catalogue rather than trying to read the loop.
    /// </summary>
    [Fact]
    public void The_generated_window_keys_are_read_from_the_catalogue()
    {
        Assert.Equal(
            DealWindowSet.All.Select(DealWindowCatalog.KeyOf).OrderBy(key => key, StringComparer.Ordinal),
            PreferencesFile.NamesIn("category.CreateEntry(DealWindowCatalog.KeyOf(window), false);"));
    }

    /// <summary>
    /// <b>The one that matters.</b> A name the guard cannot resolve stops the
    /// run. It must never be dropped, because a dropped key is a key missing
    /// from both sides of the comparison, which is a green test about nothing.
    /// </summary>
    [Fact]
    public void A_key_the_guard_cannot_read_stops_the_run_rather_than_disappearing()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => PreferencesFile.NamesIn("category.CreateEntry(nameFromSomewhereElse, false);"));

        Assert.Contains("nameFromSomewhereElse", refused.Message);
    }

    /// <summary>
    /// And a constant that resolves to nothing is the same case: an identifier
    /// the guard can parse but not value is still a key it does not know.
    /// </summary>
    [Fact]
    public void A_constant_with_no_declaration_anywhere_stops_the_run()
    {
        Assert.Throws<InvalidOperationException>(
            () => PreferencesFile.NamesIn("category.CreateEntry(SomeCatalogue.Missing, false);"));
    }

    /// <summary>
    /// Prose is not a declaration. The guard stops on what it cannot read, so a
    /// doc comment that spells out a call would otherwise break the build for
    /// saying something true.
    /// </summary>
    [Fact]
    public void A_call_written_out_in_a_comment_is_not_a_key()
    {
        Assert.Empty(PreferencesFile.NamesIn(
            "// This is the class that calls CreateEntry(whateverItLikes, false).\n"));
    }

    /// <summary>
    /// A string is not a comment. Two slashes inside a description would take
    /// the rest of the line out of a naive strip, and the key after it with them.
    /// </summary>
    [Fact]
    public void A_double_slash_inside_a_description_does_not_blind_the_guard()
    {
        // Both calls on one line: if the URL's slashes started a comment, the
        // second key would be eaten with the rest of the line.
        Assert.Equal(
            new[] { "AfterTheUrl", "AutoCounterOffer" },
            PreferencesFile.NamesIn(
                "category.CreateEntry(\"AutoCounterOffer\", false, \"see http://example.com\"); "
                + "category.CreateEntry(\"AfterTheUrl\", false);"));
    }
}
