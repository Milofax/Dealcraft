using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// One reading of what a product is worth to the customers who can order it.
/// Plain values only: the adapter fills this in from the game, the core reasons
/// about it.
/// </summary>
public sealed class ProductValuation
{
    /// <summary>
    /// False when the game could not supply the numbers, for instance because
    /// no save is loaded. Nothing is guessed in that case.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>Why the reading failed, in words a player can act on.</summary>
    public string UnavailableReason { get; set; } = string.Empty;

    /// <summary>The product's id, as the game keys it.</summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>The product's name, as the game shows it.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>What the product is worth before its properties, from the definition.</summary>
    public float BasePrice { get; set; }

    /// <summary>What the game reckons the product is worth.</summary>
    public float MarketValue { get; set; }

    /// <summary>What the player currently lists it at.</summary>
    public float AskingPrice { get; set; }

    /// <summary>Everyone who could order it, and what they would pay.</summary>
    public List<ProductCandidate> Candidates { get; } = new();

    /// <summary>
    /// The game's enjoyment score for this product across every customer known
    /// to the player, whether or not they could order it. Where
    /// <see cref="Candidates"/> answers "what would this earn", this answers
    /// "who would like it" — including the ones priced out.
    /// </summary>
    public List<float> OverallAppeals { get; } = new();

    /// <summary>
    /// Anything the reading had to step over, one sentence each: a customer
    /// whose order size the game's own limits refuse, most of all. These are not
    /// failures of the reading — the other customers still answered — but they
    /// are the only place the refused figure is ever written down, so they go to
    /// the log and to the debug record beside the decision.
    /// </summary>
    public List<string> Notes { get; } = new();

    /// <summary>
    /// How many notes a sentence carries before it starts counting instead.
    /// A whole roster refused for the same reason is one fact repeated forty
    /// times, and a forty-clause line in the log is a line nobody finishes.
    /// </summary>
    public const int MostNotesSpeltOut = 3;

    /// <summary>
    /// A decision's own sentence with anything this reading refused after it.
    /// </summary>
    /// <remarks>
    /// The notes are the half of the answer the decision cannot see: a product
    /// priced off four customers when six were asked reads exactly like a
    /// product priced off six.
    /// </remarks>
    public string Explaining(string reason)
    {
        if (Notes.Count == 0)
        {
            return reason;
        }

        int spelt = Notes.Count <= MostNotesSpeltOut ? Notes.Count : MostNotesSpeltOut;
        string said = string.Join("; ", Notes.GetRange(0, spelt));
        string rest = Notes.Count > spelt ? $"; and {Notes.Count - spelt} more like it" : string.Empty;

        return $"{reason} — {said}{rest}";
    }

    /// <summary>A reading the game refused to answer, for the stated reason.</summary>
    /// <remarks>
    /// The product is carried even though nothing was read, and that is the
    /// point of the two arguments: without them every failed reading is
    /// anonymous, the log line reads "<c>[prices] : the game broke off …</c>"
    /// with nothing before the colon, and <c>DebugRecorder</c> collapses four
    /// products into one record because they share an empty id. The owner's
    /// sessions produced exactly that.
    /// </remarks>
    public static ProductValuation Unavailable(
        string reason,
        string productId = "",
        string productName = "") =>
        new()
        {
            Available = false,
            UnavailableReason = reason,
            ProductId = productId,
            ProductName = productName,
        };
}
