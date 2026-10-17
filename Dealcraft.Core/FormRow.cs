using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>What a row of the Automation form is, and therefore how it is drawn.</summary>
/// <remarks>
/// <para>
/// Every one is a widget the cloned <c>ProductAppDetailPanel</c> already carries:
/// a switch, a toggle and a choice are its <c>ListedForSale</c> <c>Toggle</c>, a
/// stepper is its <c>ValueLabel</c> <c>InputField</c> between
/// <c>_removeButton</c> and <c>_addButton</c>.
/// </para>
/// <para>
/// <b>This used to say there is no radio widget and no draggable slider "in the
/// donor", and to mean it as a fact about the game. It is not one.</b> Schedule I
/// ships a settings-form widget family — <c>UIOption</c> over <c>UIToggle</c>,
/// <c>UISlider</c>, <c>UIHorizontalSelector</c> and <c>UIPopupSelector</c>, plus
/// 26 <c>Slider</c> members — and nobody looked at it before designing this page
/// against a product detail panel. What is true is narrower: the *product detail
/// panel* lends no radio and no slider, so a choice is drawn with that panel's
/// tick for as long as the page is cloned from it.
/// </para>
/// </remarks>
public enum FormRowKind
{
    /// <summary>One of several independent switches — a deal window.</summary>
    Toggle,

    /// <summary>
    /// One answer to a question that has exactly one answer. Its siblings are
    /// the other answers, exactly one of them is <see cref="FormRow.Chosen"/>,
    /// and pressing one writes the whole question — including pressing the one
    /// already chosen, which writes what it already says.
    /// </summary>
    Choice,

    /// <summary>A value that steps when the − or + beside it is pressed.</summary>
    Stepper,

    /// <summary>
    /// Something the form has to say rather than ask: a footnote under a
    /// question, or a setting that lives in the file only.
    /// </summary>
    Notice,
}

/// <summary>
/// One row of the Automation form: what it reads, what it currently says, and
/// what pressing it writes.
/// </summary>
/// <remarks>
/// A plain value. The renderer draws what this says and decides nothing; the
/// form decides everything and knows no Unity type exists.
/// </remarks>
public sealed class FormRow
{
    private static readonly SettingChange[] Nothing = Array.Empty<SettingChange>();

    public FormRow(
        string id,
        FormRowKind kind,
        string text,
        string value = "",
        string hint = "",
        bool chosen = false,
        int indent = 0,
        IReadOnlyList<SettingChange>? press = null,
        IReadOnlyList<SettingChange>? up = null,
        IReadOnlyList<SettingChange>? down = null,
        TypedEntry typed = default)
    {
        Id = id;
        Kind = kind;
        Text = text;
        Value = value;
        Hint = hint;
        Chosen = chosen;
        Indent = indent;
        Press = press ?? Nothing;
        Up = up ?? Nothing;
        Down = down ?? Nothing;
        Typed = typed;
    }

    /// <summary>
    /// What a press names. A setting's own row is keyed on the MelonPreferences
    /// entry; a control that steps two entries at once is keyed on the one the
    /// player would look for.
    /// </summary>
    public string Id { get; }

    public FormRowKind Kind { get; }

    /// <summary>The left-hand side: what the row names.</summary>
    public string Text { get; }

    /// <summary>The right-hand side: what it currently says. Empty where the row has none.</summary>
    public string Value { get; }

    /// <summary>
    /// One short line under the row, or empty. Never a paragraph: the one
    /// footnote a question needs that its two answers cannot say themselves.
    /// </summary>
    public string Hint { get; }

    /// <summary>Whether this switch is on, this toggle ticked, this answer chosen.</summary>
    public bool Chosen { get; }

    /// <summary>
    /// How far in from the block's edge. A setting sits inside the switch that
    /// decides whether it applies, which is the dependency drawn rather than
    /// described.
    /// </summary>
    public int Indent { get; }

    /// <summary>
    /// What pressing this row writes, in order. Empty where a press does
    /// nothing: a notice, or a setting the phone has nowhere to type into.
    /// </summary>
    /// <remarks>
    /// A list rather than one change, because a control may step a pair of
    /// entries together. Writing only the one that changed would leave the other
    /// holding a value from a rung the player has stepped off, which is the very
    /// state a paired control exists to make unreachable.
    /// </remarks>
    public IReadOnlyList<SettingChange> Press { get; }

    /// <summary>
    /// What the <c>+</c> of a <see cref="FormRowKind.Stepper"/> writes. Empty
    /// where the control is already at its top, which is what makes that end
    /// blunt rather than a wrap back to the bottom.
    /// </summary>
    public IReadOnlyList<SettingChange> Up { get; }

    /// <summary>
    /// What the <c>−</c> writes, and empty at the bottom for the same reason.
    /// </summary>
    /// <remarks>
    /// A second direction exists for exactly one control. Every other ladder in
    /// the mod wraps, and one button is enough for those.
    /// </remarks>
    public IReadOnlyList<SettingChange> Down { get; }

    /// <summary>
    /// What this row's field will accept typed into it, if anything. The
    /// default takes nothing, which is every row but the chance floor.
    /// </summary>
    public TypedEntry Typed { get; }
}
