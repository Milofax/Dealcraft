using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// What the vanilla Product Manager's own list did while Dealcraft's copy of the
/// app started.
/// </summary>
public enum ProductListState
{
    /// <summary>
    /// A count could not be taken, so nothing about the list was established.
    /// The zero value: a check that did not run reports a fault rather than a
    /// clean bill, because an unread count is exactly the case a silent pass
    /// would hide.
    /// </summary>
    Unreadable,

    /// <summary>
    /// The vanilla app holds more rows than the game holds products. There is no
    /// innocent way to that number: rows the game did not ask for are in the
    /// game's own list.
    /// </summary>
    Duplicated,

    /// <summary>
    /// The vanilla app holds fewer rows than it did when the copy was made. The
    /// other direction of the same fault, and the one the pre-rebuild notes
    /// described.
    /// </summary>
    Emptied,

    /// <summary>
    /// One row per product, and the game learned of more products in between:
    /// the vanilla app's own list arriving while the save loaded.
    /// </summary>
    FilledIn,

    /// <summary>
    /// Fewer rows than products, and none lost: the vanilla app has not caught
    /// up with the game's list yet. A state of the scene, not a fault.
    /// </summary>
    StillFilling,

    /// <summary>One row per product, and nothing moved.</summary>
    Unchanged,
}

/// <summary>
/// Two counts of the vanilla app's rows and two counts of the game's own list,
/// taken at the same two moments.
/// </summary>
/// <remarks>
/// The game's counts are the point. The rows on their own cannot say whether
/// they grew because Dealcraft's copy wrote into them or because the game
/// discovered more products, and that ambiguity is the whole of ticket 50.
/// </remarks>
public readonly struct ProductListReading
{
    /// <summary>A count that could not be taken.</summary>
    public const int Unread = -1;

    /// <summary>
    /// Whether these counts were taken at all. Set by the constructor and by
    /// nothing else, so a reading nobody filled in is four unread counts rather
    /// than four zeroes — which would otherwise read as a game holding no
    /// products and an app agreeing with it.
    /// </summary>
    private readonly bool taken;

    public ProductListReading(int rowsBefore, int thingsBefore, int rowsAfter, int thingsAfter)
    {
        RowsBefore = rowsBefore;
        ThingsBefore = thingsBefore;
        RowsAfter = rowsAfter;
        ThingsAfter = thingsAfter;
        taken = true;
    }

    /// <summary>Rows in the vanilla app when Dealcraft's copy was made.</summary>
    public int RowsBefore { get; }

    /// <summary>What the game's own list held at that same moment.</summary>
    public int ThingsBefore { get; }

    /// <summary>Rows in the vanilla app once the copy's start-up was behind it.</summary>
    public int RowsAfter { get; }

    /// <summary>What the game's own list held at that same moment.</summary>
    public int ThingsAfter { get; }

    /// <summary>Whether all four counts were taken.</summary>
    public bool Complete =>
        taken && RowsBefore >= 0 && ThingsBefore >= 0 && RowsAfter >= 0 && ThingsAfter >= 0;
}

/// <summary>
/// Whether the game's own product list was left alone, decided against the
/// game's own list rather than against what the list used to be.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the old test was wrong.</b> It asked whether the vanilla app's rows
/// had grown, and called any growth duplication. The vanilla app grows on its
/// own: <c>ProductManagerApp.Start</c> subscribes its own
/// <c>CreateEntry</c> to <c>ProductManager.onProductDiscovered</c> (the field at
/// <c>+0x120</c>, written– with a
/// delegate whose target is <c>this</c>), and
/// <c>ProductManagerLoader.Load</c> calls
/// <c>SetProductDiscovered</c> once per saved product. Its logic body adds the product and invokes that event at
///. So an app cloned while a save is loading watches the
/// vanilla app's own list fill in, and the old test reported it as duplication.
/// The owner's session is exactly that: four rows for the four default strains,
/// nineteen two frames later, twenty-two in the save.
/// </para>
/// <para>
/// <b>Why counting against the game's list covers everything.</b> A guard around
/// a window has to know when the writing happens, and ticket 23 guarded the
/// window it could measure rather than the window it happens in. This is not a
/// window. <c>CreateEntry</c> appends one row to <c>entries</c> per call
/// (, the append through
/// <c>0x98(%rsi)</c>) and nothing removes one, so a write leaves a row behind
/// whenever it happened — before <c>Start</c>, inside it, in a coroutine it
/// queued, or a minute later. Counting the rows against the game's own list
/// cannot miss one, and it can be taken again at any time.
/// </para>
/// <para>
/// <b>The assumption it stands on</b>, and it is the one to break if this ever
/// misfires: the vanilla app holds exactly one row per product the game knows.
/// That is what the two population loops and the discovery event do, and
/// nothing in <c>ProductManagerApp</c> removes an entry — there is a
/// <c>RemoveFavouriteEntry</c> and no <c>RemoveEntry</c>.
/// </para>
/// </remarks>
public static class ProductListCheck
{
    /// <summary>
    /// What the two pairs of counts say. Order matters: a count that could not
    /// be taken is answered first, because every later test would read the -1 as
    /// a number.
    /// </summary>
    public static ProductListState Look(ProductListReading reading)
    {
        if (!reading.Complete)
        {
            return ProductListState.Unreadable;
        }

        if (reading.RowsAfter > reading.ThingsAfter)
        {
            return ProductListState.Duplicated;
        }

        if (reading.RowsAfter < reading.RowsBefore)
        {
            return ProductListState.Emptied;
        }

        if (reading.RowsAfter < reading.ThingsAfter)
        {
            return ProductListState.StillFilling;
        }

        return reading.ThingsAfter > reading.ThingsBefore
            ? ProductListState.FilledIn
            : ProductListState.Unchanged;
    }

    /// <summary>
    /// Whether this is something to warn about. The three that are: rows that
    /// are not the game's, rows the game had and no longer has, and a check that
    /// could not be made.
    /// </summary>
    public static bool IsFault(ProductListState state) =>
        state is ProductListState.Unreadable
            or ProductListState.Duplicated
            or ProductListState.Emptied;

    /// <summary>
    /// What to say about it.
    /// </summary>
    /// <param name="state">The verdict, from <see cref="Look"/>.</param>
    /// <param name="reading">The counts it was reached from.</param>
    /// <param name="rows">
    /// What a row in this list is called, plural and lower case — "product rows".
    /// </param>
    /// <param name="things">
    /// What the game's own list holds, plural and lower case — "products".
    /// </param>
    /// <param name="window">
    /// When the copy's <c>Start</c> was wrapped and when the counts were taken,
    /// in the caller's words. Added to a fault only: it is what the next reader
    /// needs to know which stretch of the copy's start-up to look at, and it
    /// says nothing worth saying when nothing is wrong.
    /// </param>
    public static string Sentence(
        ProductListState state,
        ProductListReading reading,
        string rows,
        string things,
        string window)
    {
        string said = Said(state, reading, rows, things);
        return IsFault(state) && !string.IsNullOrEmpty(window)
            ? said + " — " + window
            : said;
    }

    private static string Said(
        ProductListState state,
        ProductListReading reading,
        string rows,
        string things)
    {
        string rowsAfter = Count(reading.RowsAfter);
        string thingsAfter = Count(reading.ThingsAfter);

        switch (state)
        {
            case ProductListState.Duplicated:
                return $"the vanilla Product Manager holds {rowsAfter} {rows} for the game's "
                    + $"{thingsAfter} {things}, so {Count(reading.RowsAfter - reading.ThingsAfter)} "
                    + $"of them are not the game's: something in Dealcraft's copy wrote into the "
                    + $"vanilla app's own list, and it is duplicated until the save is reloaded";

            case ProductListState.Emptied:
                return $"the vanilla Product Manager held {Count(reading.RowsBefore)} {rows} when "
                    + $"Dealcraft's copy was made and holds {rowsAfter} now, so the copy's start-up "
                    + $"took rows out of the game's own list";

            case ProductListState.FilledIn:
                return $"the vanilla Product Manager held {Count(reading.RowsBefore)} of the game's "
                    + $"{Count(reading.ThingsBefore)} {things} when Dealcraft's copy was made and "
                    + $"holds {rowsAfter} of {thingsAfter} now: its own list was arriving while the "
                    + $"save loaded, one row per entry, and none of those rows is Dealcraft's";

            case ProductListState.StillFilling:
                return $"the vanilla Product Manager holds {rowsAfter} {rows} for the game's "
                    + $"{thingsAfter} {things}, which is its own list still arriving";

            case ProductListState.Unchanged when reading.ThingsAfter == 0:
                return $"the vanilla Product Manager holds no {rows} and the game knows no "
                    + $"{things} yet";

            case ProductListState.Unchanged:
                return $"the vanilla Product Manager holds one row for each of the game's "
                    + $"{thingsAfter} {things}, the same as when Dealcraft's copy was made";

            default:
                return $"the vanilla Product Manager's {rows} or the game's own list of {things} "
                    + $"could not be counted, so whether Dealcraft's copy left that list alone was "
                    + $"not established this run";
        }
    }

    /// <summary>
    /// A count that could not be taken prints as a word rather than as -1, so a
    /// log line never reads as the game holding minus one product.
    /// </summary>
    private static string Count(int value) =>
        value < 0 ? "an unknown number of" : value.ToString(CultureInfo.InvariantCulture);
}
