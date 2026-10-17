using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.Phone.ContactsApp;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using UnityEngine;
using UnityEngine.UI;

namespace Dealcraft.Phone;

/// <summary>
/// The handles Dealcraft needs into a cloned Product Manager app. Every one of
/// them comes from a serialized field of the app's own components, never from a
/// child index or a path, because the scene cannot be inspected from here and a
/// guessed hierarchy is a guessed bug.
/// </summary>
internal sealed class AppLayout
{
    private AppLayout(ProductManagerApp app)
    {
        App = app;
    }

    public ProductManagerApp App { get; private set; }

    /// <summary>The app's screen area, from <c>App&lt;T&gt;.appContainer</c>.</summary>
    public RectTransform AppContainer { get; private set; }

    /// <summary>The master list row, from <c>ProductManagerApp.EntryPrefab</c>.</summary>
    public GameObject RowTemplate { get; private set; }

    /// <summary>A collapsible section, from <c>ProductManagerApp.ProductTypeContainers</c>.</summary>
    public GameObject SectionTemplate { get; private set; }

    /// <summary>Where the sections sit, and therefore what lays them out.</summary>
    public Transform SectionParent { get; private set; }

    /// <summary>
    /// The scroll view that moves the master list, found by walking up from the
    /// sections rather than by a path, and accepted only if the sections are
    /// inside what it moves. Null if the donor does not scroll its list, which is
    /// a layout this build does not know and not a fault.
    /// </summary>
    public ScrollRect ListScroll { get; private set; }

    /// <summary>
    /// The nearest scroll view above the sections, whether or not it is the one
    /// moving them. Not something to write a position to — it is kept so a scroll
    /// view this build cannot use can still be copied for its feel, and so the
    /// log can name it.
    /// </summary>
    public ScrollRect NearestScroll { get; private set; }

    public ProductAppDetailPanel Detail { get; private set; }

    /// <summary>The detail panel's filled state, from <c>ProductAppDetailPanel.Container</c>.</summary>
    public GameObject DetailContainer { get; private set; }

    /// <summary>The vanilla "nothing selected" display. Reused as-is.</summary>
    public GameObject NothingSelected { get; private set; }

    /// <summary>Heading type, from <c>ProductAppDetailPanel.NameLabel</c>.</summary>
    public Text HeadingTemplate { get; private set; }

    /// <summary>Body type, from <c>ProductAppDetailPanel.DescLabel</c>.</summary>
    public Text BodyTemplate { get; private set; }

    /// <summary>
    /// The donor panel's own controls, captured before the app's content is
    /// cleared. Everything the settings page draws is one of these.
    /// </summary>
    public DonorControls Controls { get; private set; } = new();

    /// <summary>Every section container the donor owns, so they can be hidden.</summary>
    public ProductTypeContainer[] OwnSections { get; private set; } = Array.Empty<ProductTypeContainer>();

    /// <summary>
    /// The colour a row's plate is drawn in, from the row template itself. Only
    /// the deselected one is left: the selected colour marked the contract rows,
    /// and nothing on this page is selected in that sense any more.
    /// </summary>
    public Color DeselectedColor { get; private set; } = Color.grey;

    /// <summary>
    /// The type the app's page is set in, all of it the donor app's own: its
    /// heading, its body label and the colours it writes them in.
    /// </summary>
    public DetailStyle Style { get; private set; } = new();

    /// <summary>
    /// Read every handle, or say which one could not be read.
    /// </summary>
    /// <remarks>
    /// Each phase names the piece it is after before it touches the scene, and a
    /// throw is reported against that name. A read of the donor app can fail in
    /// ways that have nothing to say for themselves — an interop call that
    /// cannot be JITted names a helper method and no part of the phone — so the
    /// piece being read is recorded here rather than inferred from a stack later.
    /// </remarks>
    public static bool TryRead(ProductManagerApp app, Action<string> warn, out AppLayout layout)
    {
        string piece = "the Product Manager itself";
        try
        {
            return Read(app, warn, ref piece, out layout);
        }
        catch (Exception error)
        {
            warn($"reading {piece} off the Product Manager failed, so none of the app's "
                + $"content was built: {error}");
            layout = null;
            return false;
        }
    }

    private static bool Read(ProductManagerApp app, Action<string> warn, ref string piece, out AppLayout layout)
    {
        piece = "the app's own serialized fields";
        layout = new AppLayout(app)
        {
            AppContainer = app.appContainer,
            RowTemplate = app.EntryPrefab,
            Detail = app.DetailPanel,
        };

        if (layout.AppContainer == null)
        {
            warn("the app has no appContainer; the phone layout is not what this build expects");
            return false;
        }

        if (layout.RowTemplate == null)
        {
            warn("the Product Manager has no EntryPrefab; there is no row to copy");
            return false;
        }

        piece = "the app's section containers";
        Il2CppArrayBase<ProductTypeContainer> sections =
            layout.AppContainer.GetComponentsInChildren<ProductTypeContainer>(true);
        var own = new ProductTypeContainer[sections.Length];
        for (int i = 0; i < sections.Length; i++)
        {
            own[i] = sections[i];
        }

        layout.OwnSections = own;

        if (own.Length == 0)
        {
            warn("the Product Manager has no section containers; there is no section to copy");
            return false;
        }

        // Prefer a drug-type section over the Favourites one: the favourites
        // header is decorated differently in the vanilla layout.
        ProductTypeContainer template = own[0];
        foreach (ProductTypeContainer candidate in own)
        {
            if (candidate != app.FavouritesContainer)
            {
                template = candidate;
                break;
            }
        }

        piece = "the scroll view around the master list";
        layout.SectionTemplate = template.gameObject;
        layout.SectionParent = template.transform.parent;

        // The one that moves the sections, which is not the same question as the
        // one nearest above them: a page can sit inside a scroll view of its own,
        // and a position written to that one moves the page rather than the list.
        // Inactive ancestors are included, because this runs while the app is
        // closed and the walk goes up through a switched-off phone.
        layout.ListScroll = ScrollProbe.Owning(layout.SectionParent);
        layout.NearestScroll = Interop.FindInParents<ScrollRect>(layout.SectionParent);

        if (layout.ListScroll == null && layout.NearestScroll != null)
        {
            warn($"the master list's sections sit under '{ScrollProbe.Path(layout.NearestScroll.transform)}' "
                + "but are not inside what it moves, so putting that view back to its top would "
                + "not move them; the list is left where the app puts it");
        }
        else if (layout.ListScroll == null)
        {
            warn("the master list is not inside a scroll view, so it cannot be put back "
                + "to the top when the app opens");
        }

        if (layout.Detail == null)
        {
            warn("the Product Manager has no detail panel; there is no detail layout to copy");
            return false;
        }

        piece = "the detail panel's own serialized fields";
        layout.DetailContainer = layout.Detail.Container;
        layout.NothingSelected = layout.Detail.NothingSelected;
        layout.HeadingTemplate = layout.Detail.NameLabel;
        layout.BodyTemplate = layout.Detail.DescLabel != null ? layout.Detail.DescLabel : layout.Detail.NameLabel;

        if (layout.DetailContainer == null || layout.HeadingTemplate == null)
        {
            warn("the detail panel has no container or no name label; there is no text to copy");
            return false;
        }

        piece = "the detail panel's own controls";
        layout.Controls = new DonorControls
        {
            Toggle = layout.Detail.ListedForSale != null
                ? layout.Detail.ListedForSale
                : layout.Detail.FavouriteProduct,
            Value = layout.Detail.ValueLabel,
            Add = layout.Detail._addButton,
            Remove = layout.Detail._removeButton,
            Bar = layout.Detail.AddictionSlider,
        };

        if (!layout.Controls.Complete)
        {
            warn($"the detail panel lends {layout.Controls.Missing}, so the settings page draws "
                + "what it can out of the list row instead");
        }

        piece = "the row template's colour";
        ProductEntry row = layout.RowTemplate.GetComponent<ProductEntry>();
        if (row != null)
        {
            layout.DeselectedColor = row.DeselectedColor;
        }

        piece = "the phone's product palette";
        ColorFont palette = Palette();

        piece = "the type the app's page is set in";
        layout.Style = DetailStyle.Of(layout.HeadingTemplate, layout.BodyTemplate, palette);

        return true;
    }

    /// <summary>
    /// The phone's product palette. It is held by the Contacts panel rather than
    /// by the Product Manager, so it is fetched from there. (The field name is
    /// misspelled in the game; it is matched as the game spells it.)
    /// </summary>
    private static ColorFont Palette()
    {
        var contacts = UnityEngine.Object.FindObjectOfType<ContactsDetailPanel>(true);
        return contacts != null ? contacts._proudctColorFont : null;
    }
}
