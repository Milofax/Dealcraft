using Il2CppScheduleOne.Product;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dealcraft.Phone;

/// <summary>
/// A copy of the Product Manager's own list row, taken apart into the pieces
/// Dealcraft puts back together.
/// </summary>
/// <remarks>
/// <para>
/// The app draws two quite different things out of this one widget — a row in
/// the master list, and a control on the settings page — and they want the same
/// pieces for opposite reasons. A list row wants the frame and the selection
/// outline and no tick; a switch wants the tick and no outline. Taking the tile
/// apart is the same work either way, and it was written twice before this
/// existed.
/// </para>
/// <para>
/// Every piece is the game's own. Nothing here creates a sprite, a font or a
/// colour: it keeps what the tile already had, switches off what only means
/// something for a product, and removes the component that would otherwise put
/// a product back into them.
/// </para>
/// </remarks>
internal sealed class ProductRowParts
{
    /// <summary>The left-hand label. The tile's own where it has one.</summary>
    public Text Title;

    /// <summary>The right-hand label, for the figure or the state beside the name.</summary>
    public Text Value;

    /// <summary>The tile's background, which is what a selected row is coloured.</summary>
    public Image Frame;

    /// <summary>The vanilla selection outline.</summary>
    public GameObject Outline;

    /// <summary>The tile's "listed" mark. A switch that is on shows this.</summary>
    public RectTransform Tick;

    /// <summary>The tile's "not listed" mark. A switch that is off shows this.</summary>
    public RectTransform Cross;

    /// <summary>The tile's own button, covering the whole row.</summary>
    public Button Button;

    /// <summary>
    /// The tile's second button — its "move to details" one — which covers the
    /// row as well and would otherwise answer a click this one did not.
    /// </summary>
    public Button Alternate;

    /// <summary>Whether anything on this row answers a press at all.</summary>
    public bool Pressable => Button != null || Alternate != null;

    /// <summary>
    /// Take a cloned row apart.
    /// </summary>
    /// <param name="rowObject">The clone. Never the template itself.</param>
    /// <param name="bodyTemplate">
    /// A label to copy where the tile turns out to carry fewer than the two this
    /// needs, so a row always has somewhere to write its name and its value.
    /// </param>
    /// <remarks>
    /// <para>
    /// The frame, the outline and the buttons are shaped for a square, because a
    /// product is an icon. In a row they have to cover the row: a selection
    /// outline drawn as a small box in the middle of a wide line is the giveaway
    /// that this used to be something else.
    /// </para>
    /// <para>
    /// The frame and the outline are moved up to the row and put behind
    /// everything on it, in that order, so the highlight ends exactly where the
    /// row ends and sits under the name rather than across it. The buttons keep
    /// where they are and are only stretched: they draw nothing, and the area
    /// that answers a click is the whole row either way.
    /// </para>
    /// <para>
    /// The tick and the cross arrive switched off. A list row leaves them so; a
    /// control on the settings page shows one of them. Their labels are kept out
    /// of the search for the row's own, so a row never writes its name into
    /// something that is not visible.
    /// </para>
    /// </remarks>
    public static ProductRowParts TakeApart(GameObject rowObject, Text bodyTemplate)
    {
        var parts = new ProductRowParts();
        ProductEntry template = rowObject.GetComponent<ProductEntry>();
        Text[] texts;

        if (template == null)
        {
            // A row template this build does not know. Whatever labels it has are
            // still labels, and the row goes up with them.
            texts = Widgets.TextsOutside(rowObject);
            parts.Button = rowObject.GetComponent<Button>();
        }
        else
        {
            parts.Frame = template.Frame;
            parts.Outline = template.Outline;
            parts.Tick = template.Tick;
            parts.Cross = template.Cross;
            parts.Button = template.Button;
            parts.Alternate = template.MoveToDetailsButton;

            Widgets.SetActive(template.FavouriteButton, false);
            Widgets.SetActive(template.ListingButton, false);
            Widgets.SetActive(template.Tick, false);
            Widgets.SetActive(template.Cross, false);
            if (template.Icon != null)
            {
                template.Icon.enabled = false;
            }

            Transform row = rowObject.transform;
            Widgets.PlaceBehind(
                row,
                parts.Frame,
                parts.Outline != null ? parts.Outline.transform : null);
            Widgets.StretchOver(parts.Button, row);
            Widgets.StretchOver(parts.Alternate, row);

            texts = Widgets.TextsOutside(
                rowObject,
                template.FavouriteButton,
                template.ListingButton,
                template.Tick,
                template.Cross);

            Object.Destroy(template);

            if (parts.Button == null)
            {
                parts.Button = rowObject.GetComponent<Button>();
            }
        }

        parts.Bind(rowObject, texts, bodyTemplate);
        return parts;
    }

    /// <summary>
    /// Give the row its two labels, reusing the tile's own where it has them so
    /// the type, the colour and the material stay the game's.
    /// </summary>
    /// <remarks>
    /// Both are moved up to the row before anything measures them: two columns of
    /// a row are measured against the row, and a label left inside one of the
    /// tile's inner boxes would be measured against that instead. They go last in
    /// sibling order, because sibling order is draw order and the frame and the
    /// outline were just put first — a label left among them would be drawn over
    /// by the very highlight it is supposed to sit on.
    /// </remarks>
    private void Bind(GameObject rowObject, Text[] texts, Text bodyTemplate)
    {
        Title = texts.Length > 0
            ? texts[0]
            : Widgets.CloneText(bodyTemplate, rowObject.transform, "Label", string.Empty);

        Value = texts.Length > 1
            ? texts[1]
            : Widgets.CloneText(Title, rowObject.transform, "Value", string.Empty);

        // Anything else the tile displayed belongs to a product.
        for (int i = 2; i < texts.Length; i++)
        {
            texts[i].enabled = false;
        }

        Title.rectTransform.SetParent(rowObject.transform, false);
        Value.rectTransform.SetParent(rowObject.transform, false);
        Title.rectTransform.SetAsLastSibling();
        Value.rectTransform.SetAsLastSibling();
    }
}
