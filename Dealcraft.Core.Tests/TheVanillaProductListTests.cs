using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Ticket 50. The game's own Products app must hold the same rows after
/// Dealcraft's copy has started as it would have held without it, and the guard
/// that says so must be able to tell a list that is filling from a list that is
/// duplicated.
/// </summary>
/// <remarks>
/// <para>
/// Driven from the model, not from the screen: the numbers here are the vanilla
/// app's row counts and the game's own product counts, which is what
/// <c>PhoneApp</c> reads off <c>ProductManagerApp.entries</c> and
/// <c>ProductManager.DiscoveredProducts</c>.
/// </para>
/// <para>
/// The owner's session is the case that named the ticket, and it has its own
/// test below: four rows, nineteen two frames later, and no duplication in it
/// anywhere.
/// </para>
/// </remarks>
public class TheVanillaProductListTests
{
    private const string Rows = "product rows";
    private const string Things = "products";

    [Fact]
    public void A_list_that_gained_rows_because_the_game_gained_products_is_not_duplicated()
    {
        // The owner's own run: MelonLoader/Latest.log 22:31:10.921 to 22:31:12.467,
        // with Saves/.../SaveGame_1/Products.json holding 22 discovered products
        // whose first four are the default strains.
        var reading = new ProductListReading(rowsBefore: 4, thingsBefore: 4, rowsAfter: 19, thingsAfter: 19);

        Assert.Equal(ProductListState.FilledIn, ProductListCheck.Look(reading));
        Assert.False(ProductListCheck.IsFault(ProductListState.FilledIn));
    }

    [Fact]
    public void The_rows_the_copy_built_landing_in_the_vanilla_list_is_duplication()
    {
        // What the ticket feared: the copy's nineteen rows on top of the four
        // the vanilla app already held.
        var reading = new ProductListReading(rowsBefore: 4, thingsBefore: 19, rowsAfter: 23, thingsAfter: 19);

        Assert.Equal(ProductListState.Duplicated, ProductListCheck.Look(reading));
        Assert.True(ProductListCheck.IsFault(ProductListState.Duplicated));
    }

    [Fact]
    public void One_row_too_many_is_duplication()
    {
        // The test is an invariant, not a threshold. A single stray row is a row
        // the game did not ask for.
        var reading = new ProductListReading(rowsBefore: 22, thingsBefore: 22, rowsAfter: 23, thingsAfter: 22);

        Assert.Equal(ProductListState.Duplicated, ProductListCheck.Look(reading));
    }

    [Fact]
    public void Rows_taken_out_of_the_game_s_list_are_a_fault_too()
    {
        // The other direction, and the one visual-findings.md described before
        // the rebuild: the copy clearing rows that were not its own.
        var reading = new ProductListReading(rowsBefore: 22, thingsBefore: 22, rowsAfter: 0, thingsAfter: 22);

        Assert.Equal(ProductListState.Emptied, ProductListCheck.Look(reading));
        Assert.True(ProductListCheck.IsFault(ProductListState.Emptied));
    }

    [Fact]
    public void A_vanilla_app_that_has_not_built_its_list_yet_is_waited_for_rather_than_reported()
    {
        // The copy can be made before the vanilla app's own Start has run: the
        // app type's singleton is claimed in Awake, a frame earlier.
        var reading = new ProductListReading(rowsBefore: 0, thingsBefore: 22, rowsAfter: 0, thingsAfter: 22);

        Assert.Equal(ProductListState.StillFilling, ProductListCheck.Look(reading));
        Assert.False(ProductListCheck.IsFault(ProductListState.StillFilling));
    }

    [Fact]
    public void A_list_that_is_behind_the_game_s_and_catching_up_is_not_a_fault()
    {
        var reading = new ProductListReading(rowsBefore: 4, thingsBefore: 4, rowsAfter: 19, thingsAfter: 22);

        Assert.Equal(ProductListState.StillFilling, ProductListCheck.Look(reading));
    }

    [Fact]
    public void Nothing_moving_reads_as_nothing_moving()
    {
        var reading = new ProductListReading(rowsBefore: 22, thingsBefore: 22, rowsAfter: 22, thingsAfter: 22);

        Assert.Equal(ProductListState.Unchanged, ProductListCheck.Look(reading));
        Assert.False(ProductListCheck.IsFault(ProductListState.Unchanged));
    }

    [Fact]
    public void A_count_that_could_not_be_taken_is_reported_rather_than_passed()
    {
        // Every one of the four, one at a time: a guard that could not run must
        // not read as a guard that found nothing.
        Assert.Equal(
            ProductListState.Unreadable,
            ProductListCheck.Look(new ProductListReading(ProductListReading.Unread, 4, 19, 19)));
        Assert.Equal(
            ProductListState.Unreadable,
            ProductListCheck.Look(new ProductListReading(4, ProductListReading.Unread, 19, 19)));
        Assert.Equal(
            ProductListState.Unreadable,
            ProductListCheck.Look(new ProductListReading(4, 4, ProductListReading.Unread, 19)));
        Assert.Equal(
            ProductListState.Unreadable,
            ProductListCheck.Look(new ProductListReading(4, 4, 19, ProductListReading.Unread)));

        Assert.True(ProductListCheck.IsFault(ProductListState.Unreadable));
    }

    [Fact]
    public void An_unread_count_is_never_mistaken_for_a_list_that_lost_rows()
    {
        // -1 is smaller than every real count, so an unread "after" would read
        // as Emptied if the unreadable case were not answered first.
        var reading = new ProductListReading(22, 22, ProductListReading.Unread, 22);

        Assert.Equal(ProductListState.Unreadable, ProductListCheck.Look(reading));
    }

    [Fact]
    public void The_unread_case_is_the_default_so_a_check_that_never_ran_is_a_fault()
    {
        // Both defaults, because either one landing on "nothing is wrong" would
        // turn a check nobody made into a clean bill. Four zeroes are a real
        // answer — an empty game — so the reading remembers whether it was
        // filled in at all.
        Assert.Equal(ProductListState.Unreadable, default(ProductListState));
        Assert.Equal(ProductListState.Unreadable, ProductListCheck.Look(default));
    }

    [Fact]
    public void The_duplication_warning_says_how_many_rows_are_not_the_game_s()
    {
        string said = ProductListCheck.Sentence(
            ProductListState.Duplicated,
            new ProductListReading(4, 19, 23, 19),
            Rows,
            Things,
            "the copy's Start ran wrapped from frame 8241 to frame 8242");

        Assert.Contains("holds 23 product rows for the game's 19 products", said);
        Assert.Contains("4 of them are not the game's", said);
        Assert.Contains("duplicated until the save is reloaded", said);
        Assert.Contains("wrapped from frame 8241 to frame 8242", said);
    }

    [Fact]
    public void The_filled_in_line_says_both_counts_at_both_moments()
    {
        string said = ProductListCheck.Sentence(
            ProductListState.FilledIn,
            new ProductListReading(4, 4, 19, 19),
            Rows,
            Things,
            "the copy's Start ran wrapped from frame 8241 to frame 8242");

        Assert.Contains("held 4 of the game's 4 products", said);
        Assert.Contains("holds 19 of 19 now", said);
        Assert.Contains("none of those rows is Dealcraft's", said);
    }

    [Fact]
    public void Only_a_fault_carries_the_window_the_copy_started_in()
    {
        // The window is there so a reader knows which stretch of the copy's
        // start-up to look at. With nothing wrong there is nothing to look at.
        string said = ProductListCheck.Sentence(
            ProductListState.FilledIn,
            new ProductListReading(4, 4, 19, 19),
            Rows,
            Things,
            "the copy's Start ran wrapped from frame 8241 to frame 8242");

        Assert.DoesNotContain("frame 8241", said);
    }

    [Fact]
    public void An_unread_count_never_prints_as_minus_one()
    {
        string said = ProductListCheck.Sentence(
            ProductListState.Unreadable,
            new ProductListReading(4, 4, ProductListReading.Unread, 19),
            Rows,
            Things,
            "the wrap around the copy's own Start was never installed");

        Assert.DoesNotContain("-1", said);
        Assert.Contains("could not be counted", said);
        Assert.Contains("the wrap around the copy's own Start was never installed", said);
    }

    [Fact]
    public void The_favourites_list_is_checked_in_its_own_words()
    {
        // The copy builds two lists, and the second one is counted with the
        // same rule and named for what it holds.
        string said = ProductListCheck.Sentence(
            ProductListState.FilledIn,
            new ProductListReading(0, 0, 1, 1),
            "favourite rows",
            "favourited products",
            string.Empty);

        Assert.Contains("favourited products", said);
        Assert.DoesNotContain("product rows", said);
    }

    [Fact]
    public void An_empty_game_does_not_read_as_a_row_for_each_of_nothing()
    {
        string said = ProductListCheck.Sentence(
            ProductListState.Unchanged,
            new ProductListReading(0, 0, 0, 0),
            Rows,
            Things,
            string.Empty);

        Assert.Contains("knows no products yet", said);
    }
}
