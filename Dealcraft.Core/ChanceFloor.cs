using System;
using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// The one number the player sets about negotiating: the chance below which a
/// counter is not worth making to them.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a floor and not a target, and that is the whole of the design.</b>
/// <see cref="ConfidenceSearch.Best"/> draws a curve at every rung and keeps the
/// one that earns most; no threshold enters that ranking, because the confidence
/// that pays best is a property of <em>this customer's</em> curve and was never
/// the player's number to choose. A number that overrode the search would make
/// the player poorer by construction — the case is in
/// <see cref="CounterofferGate.Send"/>: a $100 offer countered at $110 with 0.9
/// confidence is an expected $99. So the search keeps choosing, and this refuses
/// the winner afterwards when it is too risky for this player.
/// </para>
/// <para>
/// <b>The comparison is unclamped and it belongs to
/// <see cref="OfferAcceptance.ClearsThreshold"/>.</b> The game's own chance can
/// exceed 1, and <see cref="Figures.Fraction"/> and <see cref="Figures.Percent"/>
/// both hold a figure inside 0..1 — routed through either of them, a chance of
/// 101% would be indistinguishable from 100% and the best offers of all would be
/// refused at the top of the control. <see cref="Spelled"/> does not clamp
/// either, so a floor is drawn as what it is.
/// </para>
/// <para>
/// <b>There is no relationship correction and no hook for one.</b>
/// the project notes establishes that the probe and the real
/// decision do not compute the same quantity: <c>EvaluateCounteroffer</c>
/// accepts when <c>Random.Range(0, 0.9) + 0.2 × max(addiction, relationship)</c>
/// beats a threshold the probe never computes. No function of the probe's output
/// and the relationship recovers that, so the mod's number is the game's number
/// and this control is the honest thing built on top of it. The footnote the
/// drawing once carried under this control — <em>"counts the relationship; the
/// game's figure does not"</em> — is false on both halves and is not drawn.
/// </para>
/// </remarks>
public static class ChanceFloor
{
    /// <summary>The MelonPreferences entry, and the row's identity.</summary>
    /// <remarks>
    /// The key the acceptance threshold has always had. It is the same setting:
    /// it was a threshold the search settled for and it is now a threshold the
    /// search's answer is held to, so renaming it would leave an orphan in every
    /// preferences file on disk for a change of meaning the description states.
    /// </remarks>
    public const string Key = "AcceptanceProbabilityThreshold";

    /// <summary>What the control is called on the page.</summary>
    public const string Title = "Never offer below this chance";

    /// <summary>
    /// The bottom of the control. Not a new rule:
    /// <see cref="ConfidenceSearch.Rungs"/> already starts here, and below a
    /// half a counter is a coin toss with money already on the table.
    /// </summary>
    public const float Lowest = 0.5f;

    /// <summary>
    /// The top of the control. <see cref="ConfidenceSearch.Rungs"/> reaches it,
    /// which is what makes 100% mean "only when the customer is certain" rather
    /// than "never".
    /// </summary>
    public const float Highest = 1f;

    /// <summary>How far one press of the control's <c>+</c> or <c>−</c> moves it.</summary>
    public const float Step = 0.05f;

    /// <summary>
    /// How many of the units the player types make one of the units the file
    /// holds. The file holds a chance, 0..1, because that is what the game's
    /// own figure is; the player reads and types whole percent, because that is
    /// what the game shows them.
    /// </summary>
    public const float PerTypedUnit = 100f;

    /// <summary>The bottom of the control as the player types it.</summary>
    public const float LowestTyped = Lowest * PerTypedUnit;

    /// <summary>The top of the control as the player types it.</summary>
    public const float HighestTyped = Highest * PerTypedUnit;

    /// <summary>
    /// A floor as the control shows it: whole percent, and never clamped — a
    /// figure outside the control's own range is drawn as what it is rather than
    /// as the nearest end, so a file somebody edited by hand shows what it says.
    /// </summary>
    public static string Spelled(float floor) =>
        Math.Round(floor * PerTypedUnit).ToString("0", CultureInfo.InvariantCulture) + "%";
}
