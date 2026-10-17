using UnityEngine;
using UnityEngine.UI;

namespace Dealcraft.Phone;

/// <summary>
/// Which scroll view actually carries a transform, and where in the scene a
/// thing sits.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Owning"/> counts a scroll view as carrying a transform only if its
/// own <c>content</c> is that transform or an ancestor of it. The nearest
/// <see cref="ScrollRect"/> above something is not necessarily the one that
/// moves it — a page can sit inside a scroll view of its own — and "nearest" was
/// what the app had been using when it opened on the eleventh of forty-one
/// customers while the log said the list stood at the top.
/// </para>
/// <para>
/// That list is gone. What is left is read by <c>AppLayout</c>, which needs to
/// know whether the donor's own scroll view moves the donor's own sections
/// before it lends that view's feel to the settings page.
/// </para>
/// </remarks>
internal static class ScrollProbe
{
    /// <summary>
    /// The scroll view that moves <paramref name="rows"/>, or null if nothing
    /// above them scrolls them.
    /// </summary>
    /// <remarks>
    /// The walk goes up the parent chain and accepts the first scroll view whose
    /// content the rows are actually inside. A scroll view that happens to be
    /// overhead but scrolls something else is passed over rather than taken for
    /// the one to write a position to.
    /// </remarks>
    public static ScrollRect Owning(Transform rows)
    {
        for (Transform node = rows; node != null; node = node.parent)
        {
            ScrollRect scroll = node.GetComponent<ScrollRect>();
            if (scroll != null && Holds(scroll, rows))
            {
                return scroll;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="node"/> is inside what
    /// <paramref name="scroll"/> moves.
    /// </summary>
    private static bool Holds(ScrollRect scroll, Transform node)
    {
        if (scroll == null || node == null)
        {
            return false;
        }

        RectTransform content = scroll.content;
        if (content == null)
        {
            return false;
        }

        for (Transform step = node; step != null; step = step.parent)
        {
            // Compared by pointer, the way the rest of the phone's scene reads
            // are: two managed wrappers around one Il2Cpp object are two objects
            // and one scene node.
            if (step.Pointer == content.Pointer)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Where an object sits, named from the root of its scene down. A path is
    /// what somebody with the game open can find; a name on its own is not, and
    /// three of the objects in this app are called "Content".
    /// </summary>
    public static string Path(Transform node)
    {
        if (node == null)
        {
            return "none";
        }

        var text = new System.Text.StringBuilder(node.name);
        for (Transform step = node.parent; step != null; step = step.parent)
        {
            text.Insert(0, '/').Insert(0, step.name);
        }

        return text.ToString();
    }
}
