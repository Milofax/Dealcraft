using System;
using System.Collections.Generic;
using Dealcraft.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dealcraft.Phone;

/// <summary>
/// The app's page: an <see cref="AutomationForm"/> drawn out of the donor
/// panel's own controls. It is the whole of what the app shows — there is no
/// strip and no second screen behind it.
/// </summary>
/// <remarks>
/// <para>
/// Every control here is a part of something already in the scene, and all of
/// them come from the one component the app already clones. A switch and a
/// window toggle are the panel's <c>ListedForSale</c> <c>Toggle</c>; a figure is
/// its <c>ValueLabel</c> input field between its own <c>−</c> and <c>+</c>. No
/// asset ships and nothing is styled by hand.
/// </para>
/// <para>
/// <b>The chance floor is drawn with that stepper, and it is the second rung of
/// the ladder <c>app.md</c> wrote down rather than the first.</b> The first is
/// the game's own <c>UISlider</c> family, which exists — the feasibility review
/// found <c>UI.Management.NumberFieldUI</c> part for part, and
/// <c>ContactsApp.InfluenceSlider</c> on the phone itself — but whether it drags
/// cleanly inside this page's own <c>ScrollRect</c> is reasoned from the type
/// and has never been measured, and nobody has had this page on a screen. The
/// stepper is the rung that is proven: it is cloned and drawn in this build
/// already. Climbing back to the slider is a donor and a drag test, not a
/// redesign — the form hands out the same row either way.
/// </para>
/// <para>
/// Nothing here decides anything either. The form says which rows exist, what
/// each reads, whether it is on and what pressing it writes; this puts those on
/// screen and hands a press back. A setting that does not currently apply never
/// reaches this class at all, which is what makes "hidden while the switch is
/// off" true rather than a rule somebody has to remember — and the verdict each
/// block carries is the string its own gate returned, not a sentence written for
/// this screen.
/// </para>
/// <para>
/// Whose machine the page belongs to is decided there too. On a guest the form
/// hands over one block and the note that says the other three are set on the
/// host and are working for his deals; this draws what it is given and asks no
/// questions about it. The only thing here that knows about either is the word
/// at the right-hand end of the title bar, which is the form's as well.
/// </para>
/// </remarks>
internal sealed class AutomationView
{
    /// <summary>How far in one level of nesting sits.</summary>
    private const int IndentStep = 18;

    /// <summary>The air between two rows of the form.</summary>
    private const float RowSpacing = 3f;

    /// <summary>The air between two blocks.</summary>
    private const float BlockSpacing = 14f;

    /// <summary>The inset a row's text keeps from the row's own edges.</summary>
    private const float RowPadding = 8f;

    /// <summary>The gap kept between a row's name and what is beside it.</summary>
    private const float ColumnGap = 12f;

    private readonly AppLayout _layout;
    private readonly Func<string, bool> _press;
    private readonly Func<string, bool, bool> _step;
    private readonly Func<string, string, bool> _typed;
    private readonly Action<string> _log;
    private readonly Action<string> _warn;
    private readonly SaidOnce _said = new();

    /// <summary>
    /// Il2Cpp holds our handlers by native pointer only. Keeping the managed
    /// delegates alive here is what stops the collector from pulling the ground
    /// out from under them.
    /// </summary>
    private readonly List<Action> _handlers = new();

    private readonly List<Action<bool>> _toggleHandlers = new();

    private readonly List<Action<string>> _typedHandlers = new();

    private RectTransform _window;
    private RectTransform _lines;

    /// <summary>
    /// The word at the right-hand end of the title bar: <c>HOST MODE</c> or
    /// <c>CLIENT MODE</c>. Not part of the form's stack — it sits in the app's
    /// own header band, beside the app's name, and does not scroll with the page.
    /// </summary>
    private Text _mode;

    private ScrollRect _scroll;
    private float _rowHeight;
    private float _headingHeight;

    /// <param name="press">
    /// A row was pressed. Answers whether anything was written, which is the
    /// app's to decide and this class's to redraw on.
    /// </param>
    /// <param name="step">
    /// One end of a stepper was pressed, with the direction it goes in.
    /// Answers the same way, and answers false at an end the control does not
    /// go past.
    /// </param>
    /// <param name="typed">
    /// Something was typed into a field and committed. False is the entry
    /// refused, and the field goes back to the value it was showing.
    /// </param>
    public AutomationView(
        AppLayout layout,
        Func<string, bool> press,
        Func<string, bool, bool> step,
        Func<string, string, bool> typed,
        Action<string> log,
        Action<string> warn)
    {
        _layout = layout;
        _press = press;
        _step = step;
        _typed = typed;
        _log = log;
        _warn = warn;
    }

    /// <summary>Whether the panel exists at all.</summary>
    public bool IsBuilt => _lines != null;

    /// <summary>
    /// Build the panel the form is drawn in: a window over the app's screen, a
    /// stack inside it, and a scroll view between the two so a form longer than
    /// the screen can be read to the end.
    /// </summary>
    public void Build(Transform parent, ScrollRect feel)
    {
        _rowHeight = RowLayout.Height(_layout.BodyTemplate != null ? _layout.BodyTemplate.fontSize : 0f);
        _headingHeight = RowLayout.Height(
            _layout.Style.Heading != null ? _layout.Style.Heading.fontSize : 0f);

        RectTransform window = Widgets.Container(_layout.BodyTemplate, parent, "Dealcraft_Automation");
        Widgets.ContentSized(window);
        Widgets.Fill(window);
        Widgets.IgnoreLayout(window);

        RectTransform stack = Widgets.Container(_layout.BodyTemplate, window, "Dealcraft_AutomationLines");
        Widgets.ContentSized(stack);

        if (!Widgets.EnsureVerticalLayout(stack.gameObject, BlockSpacing, padding: 14))
        {
            _warn("the Automation panel would not take a vertical stack, so its blocks are placed "
                + "by whatever the copied label brought with it");
        }

        _scroll = Widgets.MakeScrollView(window, stack, feel);
        if (_scroll == null)
        {
            _warn("the Automation panel would not take a scroll view, so anything past the bottom "
                + "of the screen is cut off rather than reachable");
        }

        _window = window;
        _lines = stack;
        _mode = TitleBarMode(parent);

        // Shown from the first frame: this is the app's only page.
        _window.gameObject.SetActive(true);
    }

    /// <summary>
    /// The title bar's right-hand word, in the app's own heading type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A guest's page is one block where the host's is four, and without a word
    /// saying why, a short page reads as a broken one. It sits beside the app's
    /// name rather than on the page, because it is about the whole page and not
    /// about any block of it.
    /// </para>
    /// <para>
    /// Placed against the app's own screen rather than inside the form's stack,
    /// so it stays put while the page scrolls. Where it sits vertically is
    /// whatever <see cref="StartBelow"/> measured the header band to be: nothing
    /// here knows how tall that band is, so a patch that changes the header moves
    /// this with it.
    /// </para>
    /// </remarks>
    private Text TitleBarMode(Transform parent)
    {
        Text template = _layout.Style.Heading != null ? _layout.Style.Heading : _layout.BodyTemplate;
        if (template == null || parent == null)
        {
            _warn("the app lends no label to set the title bar's word in, so the page does not "
                + "say whether it is the host's or a guest's");
            return null;
        }

        Text label = Widgets.CloneText(template, parent, "Dealcraft_Mode", string.Empty);
        label.alignment = TextAnchor.MiddleRight;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = label.rectTransform;
        Widgets.IgnoreLayout(rect);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(_rowHeight * 8f, _headingHeight);
        rect.anchoredPosition = new Vector2(-RowPadding, 0f);

        label.gameObject.SetActive(true);
        return label;
    }

    /// <summary>
    /// Start the panel <paramref name="top"/> below the top of the app, so it
    /// covers the screen and not the header band above it.
    /// </summary>
    /// <remarks>
    /// An absolute offset rather than a push, so calling it again on every look
    /// of the app's wait says the same thing rather than moving the panel a
    /// little further down each time.
    /// </remarks>
    public void StartBelow(float top)
    {
        if (_window == null || !Measurement.IsFinite(top) || top < 0f)
        {
            return;
        }

        _window.offsetMax = new Vector2(_window.offsetMax.x, -top);

        // And the title bar's word sits in the band the page starts below,
        // halfway down it.
        if (_mode != null)
        {
            _mode.rectTransform.anchoredPosition = new Vector2(
                -RowPadding, -Mathf.Max(0f, (top - _headingHeight) * 0.5f));
        }

        if (_said.ShouldSay("automation-top", $"{top:0.#}"))
        {
            _log($"the settings page begins {top:0.#} below the top of the app, under its "
                + "header band");
        }
    }

    /// <summary>
    /// Move the form to the top, so an app that has just been opened is read
    /// from its first block rather than from wherever it was left.
    /// </summary>
    public void ScrollToTop()
    {
        if (_scroll != null)
        {
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>
    /// Draw this form. Everything already on the panel goes first: a switch that
    /// has just been turned on reveals rows that were not there a moment ago, and
    /// rewriting the ones that happen to still exist would leave the rest behind.
    /// </summary>
    public void Paint(AutomationForm form)
    {
        if (_lines == null)
        {
            return;
        }

        Widgets.ClearChildren(_lines);
        _handlers.Clear();
        _toggleHandlers.Clear();
        _typedHandlers.Clear();

        if (_mode != null)
        {
            _mode.text = form.Mode;
        }

        int rows = 0;
        var drawn = new List<string>(form.Blocks.Count);

        foreach (AutomationBlock block in form.Blocks)
        {
            try
            {
                int painted = PaintBlock(block);
                rows += painted;

                if (painted > 0)
                {
                    drawn.Add($"{Named(block)} {painted}");
                }
            }
            catch (Exception error)
            {
                // One block that will not build is reported and skipped rather
                // than taken as the end of the form: four blocks minus one is
                // worth more than an empty page.
                _warn($"the '{Named(block)}' block could not be drawn, so it is not on screen: "
                    + error.Message);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_lines);

        // Named block by block, with its row count. A count of the whole page
        // cannot say whether a particular block reached the screen, and this
        // can. It is one line, and only when the page changed.
        string shape = $"{form.Mode}: " + string.Join(", ", drawn);
        if (_said.ShouldSay("form", shape))
        {
            _log($"the settings page shows {drawn.Count} block(s) and {rows} row(s) — {shape}; "
                + "a setting that does not currently apply is not among them, and on a guest "
                + "neither is a setting only the host can make");
        }
    }

    /// <summary>
    /// One block: its heading with the gate's verdict on the right, then its
    /// rows, each inside a container one level deeper than the last.
    /// </summary>
    private int PaintBlock(AutomationBlock block)
    {
        if (block.Rows.Count == 0)
        {
            // A block whose feature is not in this build. An empty container
            // would still take the air between two blocks, which would read as a
            // gap somebody forgot to fill.
            return 0;
        }

        RectTransform container = Stacked(_lines, "Block_" + Named(block), RowSpacing, left: 0);

        if (block.Title.Length > 0)
        {
            Heading(container, block);
        }

        // One container per indent level, so nesting is drawn by the layout
        // rather than by shifting text about inside a full-width row.
        var levels = new List<RectTransform> { container };

        foreach (FormRow row in block.Rows)
        {
            while (levels.Count > row.Indent + 1)
            {
                levels.RemoveAt(levels.Count - 1);
            }

            while (levels.Count < row.Indent + 1)
            {
                levels.Add(Stacked(
                    levels[levels.Count - 1],
                    $"Indent_{levels.Count}",
                    RowSpacing,
                    left: IndentStep));
            }

            PaintRow(row, levels[levels.Count - 1]);
        }

        return block.Rows.Count;
    }

    /// <summary>The block's name, in the game's own section type.</summary>
    /// <remarks>
    /// It used to carry what the block's gate said about it on the right —
    /// <c>running</c>, <c>waiting</c>, <c>no window allowed</c>. The owner
    /// deleted every one of them: <em>"Ich gehe davon aus, wenn ich es gesetzt
    /// habe, dann läuft es."</em> The gates still say those things and they go to
    /// the debug file, which is where a later reader should look for them.
    /// </remarks>
    private void Heading(RectTransform container, AutomationBlock block)
    {
        GameObject row = Widgets.Clone(_layout.RowTemplate, container, "Heading_" + Named(block));
        ProductRowParts parts = ProductRowParts.TakeApart(row, _layout.BodyTemplate);

        Widgets.SetActive(parts.Tick, false);
        Widgets.SetActive(parts.Cross, false);
        Widgets.SetActive(parts.Outline, false);
        Widgets.SetActive(parts.Button, false);
        Widgets.SetActive(parts.Alternate, false);

        // A heading is not a row and must not wear a row's plate, or the block
        // reads as a list whose first entry happens to be in capitals.
        Widgets.SetActive(parts.Frame, false);

        if (parts.Title != null)
        {
            parts.Title.text = block.Title;
            Typeset(parts.Title, _layout.Style.Heading);
        }

        Widgets.SetActive(parts.Value, false);

        Widgets.PlaceColumns(parts.Title, parts.Value, RowPadding, ColumnGap);
        Widgets.MakeRow(row, _headingHeight);
        row.SetActive(true);
    }

    private void PaintRow(FormRow row, Transform parent)
    {
        switch (row.Kind)
        {
            case FormRowKind.Notice:
                // A footnote sits inside something and is marked as belonging to
                // it; a line on the page's own margin is a sentence the page
                // says, and a bullet in front of it would read as a footnote
                // about nothing. The guest's note is the second kind.
                Label(row.Id, row.Text, row.Value, parent, bullet: row.Indent > 0);
                return;

            default:
                Control(row, parent);
                return;
        }
    }

    /// <summary>
    /// An answer, a window toggle or a stepper: a row of the product list with
    /// the donor panel's own control put on the right of it.
    /// </summary>
    private void Control(FormRow row, Transform parent)
    {
        GameObject rowObject = Widgets.Clone(_layout.RowTemplate, parent, "Control_" + row.Id);
        ProductRowParts parts = ProductRowParts.TakeApart(rowObject, _layout.BodyTemplate);

        Widgets.SetActive(parts.Outline, false);
        if (parts.Frame != null)
        {
            parts.Frame.color = _layout.DeselectedColor;
        }

        if (parts.Title != null)
        {
            parts.Title.text = row.Text;
        }

        bool ticked = row.Kind is FormRowKind.Toggle or FormRowKind.Choice;
        float taken = ticked ? Tick(rowObject.transform, row) : Stepper(rowObject.transform, row);

        if (parts.Value != null)
        {
            // An answer says what it is by its tick; a window toggle's value is
            // its clock times; a stepper draws its own, so the row's second label
            // is only used where the stepper could not be built.
            parts.Value.text = row.Kind == FormRowKind.Toggle || taken <= 0f
                ? row.Value
                : string.Empty;
            parts.Value.color = _layout.Style.Accent;
        }

        // The value column keeps clear of whatever was put at the right-hand end,
        // so a window's clock times and its tick are never drawn on top of one
        // another.
        Widgets.PlaceColumns(parts.Title, parts.Value, RowPadding + taken, ColumnGap);

        // Only a row whose control could not be built falls back to the row's own
        // button: a tick and a stepper carry their own, and two things answering
        // one press would step a setting twice.
        if (taken <= 0f)
        {
            Wire(parts.Button, row.Id);
            Wire(parts.Alternate, row.Id);
        }
        else
        {
            Widgets.SetActive(parts.Button, false);
            Widgets.SetActive(parts.Alternate, false);
        }

        Widgets.MakeRow(rowObject, _rowHeight);
        rowObject.SetActive(true);

        if (row.Hint.Length > 0)
        {
            Label(row.Id + ":hint", row.Hint, string.Empty, parent);
        }
    }

    /// <summary>
    /// The donor panel's own tick box, at the right-hand end of a row. Answers
    /// how much of the row it took, or 0 where this build lent none.
    /// </summary>
    private float Tick(Transform row, FormRow form)
    {
        Toggle donor = _layout.Controls.Toggle;
        if (donor == null)
        {
            return 0f;
        }

        GameObject clone = Widgets.Clone(donor.gameObject, row, "Toggle");
        var toggle = clone.GetComponent<Toggle>();

        if (toggle == null)
        {
            Object.Destroy(clone);
            return 0f;
        }

        // The donor's own group would make this one of a set belonging to a
        // panel that is not ours.
        toggle.group = null;
        toggle.interactable = form.Press.Count > 0;
        toggle.SetIsOnWithoutNotify(form.Chosen);

        string id = form.Id;
        var handler = new Action<bool>(_ =>
        {
            try
            {
                _press(id);
            }
            catch (Exception error)
            {
                // Il2Cpp calls this out of the toggle's own event dispatch.
                _warn($"the app could not act on '{id}': {error.Message}");
            }
        });

        _toggleHandlers.Add(handler);
        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(handler);

        clone.SetActive(true);
        return AtTheRight(clone.GetComponent<RectTransform>(), _rowHeight);
    }

    /// <summary>
    /// The donor panel's own stepper — its number between its own <c>−</c> and
    /// <c>+</c> — at the right-hand end of a row. Answers how much of the row it
    /// took, or 0 where this build lent none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two buttons step in opposite directions, and either may do
    /// nothing.</b> Every other ladder in this mod wraps, and one button is
    /// enough for those; the chance floor's ends mean something, so the form
    /// answers a press at the top or the bottom with no write at all. No write
    /// means no redraw, so the number stays where it is — which is what a blunt
    /// end looks like from the player's side.
    /// </para>
    /// <para>
    /// <b>The field is typed into.</b> It was <c>readOnly</c> and
    /// <c>interactable = false</c>, on the reasoning that a phone has nowhere to
    /// type; the owner asked for typed entry twice and in capitals, and the
    /// field is the game's own input, so it already knows how. It was switched
    /// off, not missing. A row whose form takes nothing typed keeps it switched
    /// off.
    /// </para>
    /// <para>
    /// The committed entry goes back to the form, which answers with a write or
    /// with nothing. Nothing means the entry was refused — out of range, or not
    /// a number — and the field is put back to what the file still says rather
    /// than clamped to the nearest end.
    /// </para>
    /// </remarks>
    private float Stepper(Transform row, FormRow form)
    {
        DonorControls donor = _layout.Controls;
        if (!donor.CanStep)
        {
            return 0f;
        }

        GameObject clone = Widgets.Clone(donor.Value.gameObject, row, "Value");
        var field = clone.GetComponent<InputField>();

        if (field == null)
        {
            Object.Destroy(clone);
            return 0f;
        }

        Typing(field, form);

        if (field.textComponent != null)
        {
            field.textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
            field.textComponent.alignment = TextAnchor.MiddleRight;
        }

        field.SetTextWithoutNotify(form.Value);

        var rect = clone.GetComponent<RectTransform>();
        float width = Mathf.Max(rect != null ? rect.rect.width : 0f, _rowHeight * 4f);

        // Placed from the right edge inwards, so the three read left to right as
        // the panel's own do: − then the number then +.
        float add = Step(row, donor.Add, form.Id, "Add", up: true, 0f);
        AtTheRight(rect, width, add);
        clone.SetActive(true);

        float remove = Step(
            row, donor.Remove, form.Id, "Remove", up: false, add + width + ColumnGap);

        return add + width + ColumnGap + remove;
    }

    /// <summary>
    /// Let the player type into the field, or leave it as the reading it was.
    /// </summary>
    private void Typing(InputField field, FormRow form)
    {
        if (!form.Typed.Takes)
        {
            field.readOnly = true;
            field.interactable = false;
            return;
        }

        field.readOnly = false;
        field.interactable = true;

        // The donor's limit is a price's, and this field holds a percentage.
        // Unbounded here rather than guessed at: an entry too long to be a
        // percentage is refused by the range and the field reverts, which is
        // the same answer with the reason attached.
        field.characterLimit = 0;

        string id = form.Id;
        string wasValid = form.Value;

        var handler = new Action<string>(typed =>
        {
            try
            {
                if (!_typed(id, typed))
                {
                    // Refused, so the file still says what it said. Put that
                    // back: clamping to the nearest end would turn a typo into a
                    // setting and never tell the player it was refused. An
                    // accepted entry redraws the whole page, so there is nothing
                    // to put back on that side.
                    field.SetTextWithoutNotify(wasValid);
                }
            }
            catch (Exception error)
            {
                // Il2Cpp calls this out of the field's own event dispatch.
                _warn($"the app could not act on what was typed into '{id}': {error.Message}");
            }
        });

        _typedHandlers.Add(handler);

        // Both of the donor's events, because the donor is the product panel's
        // price field and its baked-in listeners write a price.
        Widgets.Rewire(field, handler);
    }

    /// <summary>
    /// One of the stepper's two buttons, beside the number.
    /// </summary>
    /// <param name="up">
    /// Which way this one goes. The form may answer either with nothing, which
    /// is how the end of a range stays blunt instead of wrapping.
    /// </param>
    private float Step(
        Transform row, Transform donor, string id, string name, bool up, float inset)
    {
        if (donor == null)
        {
            return 0f;
        }

        GameObject clone = Widgets.Clone(donor.gameObject, row, name);
        var rect = clone.GetComponent<RectTransform>();

        if (rect == null)
        {
            Object.Destroy(clone);
            return 0f;
        }

        WireStep(Interop.FindHere<Button>(clone), id, up);

        float taken = AtTheRight(rect, _rowHeight, inset);
        clone.SetActive(true);
        return taken;
    }

    /// <summary>
    /// A line the form says rather than asks: a count a pass keeps, the one
    /// condition a reading cannot state for itself, or a setting that lives in
    /// the file only.
    /// </summary>
    /// <param name="figure">
    /// What goes on the right, or empty. A bonus the game paid has one — its own
    /// label and its own amount, in two columns — and a note about a pass does
    /// not.
    /// </param>
    /// <param name="bullet">
    /// Whether the line is marked as belonging to what is above it. True for a
    /// footnote under a control, false for a sentence the page says on its own
    /// margin.
    /// </param>
    private void Label(string id, string words, string figure, Transform parent, bool bullet = true)
    {
        if (figure.Length == 0)
        {
            Text note = Widgets.CloneText(
                _layout.Style.Body, parent, "Note_" + id, bullet ? "· " + words : words);
            note.color = _layout.Style.Muted;
            note.alignment = TextAnchor.MiddleLeft;
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.verticalOverflow = VerticalWrapMode.Overflow;
            Widgets.ContentSized(note.rectTransform);
            note.gameObject.SetActive(true);
            return;
        }

        // Two columns, because the player is comparing the figures down the
        // block: a $200 curfew bonus beside a $20 generosity one is the whole
        // point, and it only reads if they line up.
        GameObject rowObject = Widgets.Clone(_layout.RowTemplate, parent, "Note_" + id);
        ProductRowParts parts = ProductRowParts.TakeApart(rowObject, _layout.BodyTemplate);

        Widgets.SetActive(parts.Tick, false);
        Widgets.SetActive(parts.Cross, false);
        Widgets.SetActive(parts.Outline, false);
        Widgets.SetActive(parts.Frame, false);
        Widgets.SetActive(parts.Button, false);
        Widgets.SetActive(parts.Alternate, false);

        if (parts.Title != null)
        {
            parts.Title.text = words;
            parts.Title.color = _layout.Style.Muted;
        }

        if (parts.Value != null)
        {
            parts.Value.text = figure;
            parts.Value.color = _layout.Style.Accent;
        }

        Widgets.PlaceColumns(parts.Title, parts.Value, RowPadding, ColumnGap);
        Widgets.MakeRow(rowObject, _rowHeight);
        rowObject.SetActive(true);
    }

    /// <summary>
    /// Put a widget at the right-hand end of a row, <paramref name="inset"/> in
    /// from the edge. Answers how much of the row it takes, including the gap
    /// before it.
    /// </summary>
    private float AtTheRight(RectTransform rect, float width, float inset = 0f)
    {
        if (rect == null)
        {
            return 0f;
        }

        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(width, _rowHeight);
        rect.anchoredPosition = new Vector2(-(RowPadding + inset), 0f);
        rect.SetAsLastSibling();

        return width + ColumnGap;
    }

    /// <summary>Set a label in another label's type, keeping everything else.</summary>
    private static void Typeset(Text label, Text like)
    {
        if (label == null || like == null)
        {
            return;
        }

        label.font = like.font;
        label.fontSize = like.fontSize;
        label.fontStyle = like.fontStyle;
        label.color = like.color;
    }

    /// <summary>
    /// A container that stacks what is put in it, inset from the left by
    /// <paramref name="left"/> — which is how one level of nesting is drawn.
    /// </summary>
    private RectTransform Stacked(Transform parent, string name, float spacing, int left)
    {
        RectTransform container = Widgets.Container(_layout.BodyTemplate, parent, name);
        Widgets.ContentSized(container);

        if (Widgets.EnsureVerticalLayout(container.gameObject, spacing, padding: 0))
        {
            VerticalLayoutGroup stack = Widgets.Stack(container.gameObject);
            if (stack != null)
            {
                stack.spacing = spacing;
                stack.padding = new RectOffset(left, 0, 0, 0);
            }
        }

        container.gameObject.SetActive(true);
        return container;
    }

    private void Wire(Button button, string id)
    {
        if (button == null)
        {
            return;
        }

        var handler = new Action(() =>
        {
            try
            {
                _press(id);
            }
            catch (Exception error)
            {
                // Guarded because Il2Cpp invokes this straight out of the game's
                // own button dispatch, which is not ours to break.
                _warn($"the app could not act on '{id}': {error.Message}");
            }
        });

        _handlers.Add(handler);
        Widgets.Rewire(button, handler);
    }

    /// <summary>One end of a stepper, which is a press with a direction.</summary>
    private void WireStep(Button button, string id, bool up)
    {
        if (button == null)
        {
            return;
        }

        var handler = new Action(() =>
        {
            try
            {
                _step(id, up);
            }
            catch (Exception error)
            {
                _warn($"the app could not act on '{id}': {error.Message}");
            }
        });

        _handlers.Add(handler);
        Widgets.Rewire(button, handler);
    }

    /// <summary>A block's name, for an object name and the log. Never empty.</summary>
    /// <remarks>
    /// The one block with no heading is the note a guest's page opens with. The
    /// footer this used to stand in for is gone.
    /// </remarks>
    private static string Named(AutomationBlock block) =>
        block.Title.Length > 0 ? block.Title : "NOTE";
}
