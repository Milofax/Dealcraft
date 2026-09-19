using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dealcraft.Phone;

/// <summary>
/// Cloning helpers. Every widget Dealcraft shows is a copy of one that is
/// already in the scene, so the font, the colours, the sprites and the metrics
/// are the game's own and the mod ships no asset.
/// </summary>
internal static class Widgets
{
    /// <summary>
    /// Copy a scene object under <paramref name="parent"/>, keeping its local
    /// transform. The non-generic overload plus a cast is used throughout
    /// rather than <c>Instantiate&lt;T&gt;</c>, to keep one interop shape.
    /// </summary>
    public static GameObject Clone(GameObject template, Transform parent, string name)
    {
        Object created = Object.Instantiate(template, parent, false);
        GameObject clone = created.Cast<GameObject>();
        clone.name = name;
        return clone;
    }

    /// <summary>Copy a <see cref="Text"/> and put a string in it.</summary>
    public static Text CloneText(Text template, Transform parent, string name, string content)
    {
        Text clone = Clone(template.gameObject, parent, name).GetComponent<Text>();
        clone.text = content;
        return clone;
    }

    /// <summary>
    /// An empty <see cref="RectTransform"/> to group things in. Made by copying
    /// a <see cref="Text"/> and dropping the Text, because that yields a
    /// RectTransform with the canvas' own scale without constructing a bare
    /// GameObject and upgrading its Transform.
    /// </summary>
    /// <remarks>
    /// The labels are taken off immediately rather than at the end of the frame.
    /// A deferred <c>Destroy</c> leaves them attached for the rest of it, which
    /// is a frame of the template's own words showing through the container —
    /// and, because <c>Graphic</c> is <c>[DisallowMultipleComponent]</c>, a
    /// container that will not take the <see cref="Image"/> a caller is about to
    /// put on it. Unity answers that refusal with null, not an exception.
    /// </remarks>
    public static RectTransform Container(Text shapeTemplate, Transform parent, string name)
    {
        GameObject clone = Clone(shapeTemplate.gameObject, parent, name);

        foreach (Text text in clone.GetComponentsInChildren<Text>(true))
        {
            text.text = string.Empty;
            text.enabled = false;
            Object.DestroyImmediate(text);
        }

        return clone.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Take the donor's own metrics off a copy, so what it is given is decided by
    /// what is in it rather than by what the widget we copied happened to be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the half of the detail panel's layout that a vertical stack alone
    /// does not fix. A <see cref="LayoutElement"/> outranks everything else a
    /// layout group could ask — it answers at priority 1, a <see cref="Text"/>
    /// answers at 0 — so a clone that inherited one with a height baked into it
    /// reports that height however much is written in it. The stack then does
    /// exactly as it is told: a wrapped value gets one line of room and is drawn
    /// through the rows beneath it.
    /// </para>
    /// <para>
    /// A <see cref="ContentSizeFitter"/> is switched off for the same reason and
    /// one step further along: inside a group that controls its children's sizes
    /// it is a second thing setting the same number, a frame later.
    /// </para>
    /// <para>
    /// The element itself is left attached rather than destroyed, so a caller can
    /// go on to put a height on it with <see cref="SetHeight"/> — and because
    /// taking a component off only takes effect at the end of the frame, which is
    /// the trap this file already carries two other scars from.
    /// </para>
    /// </remarks>
    public static void ContentSized(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        LayoutElement element = rect.GetComponent<LayoutElement>();
        if (element != null)
        {
            element.ignoreLayout = false;
            element.minWidth = -1f;
            element.minHeight = -1f;
            element.preferredWidth = -1f;
            element.preferredHeight = -1f;
            element.flexibleWidth = -1f;
            element.flexibleHeight = -1f;
        }

        foreach (ContentSizeFitter fitter in rect.GetComponents<ContentSizeFitter>())
        {
            fitter.enabled = false;
        }
    }

    /// <summary>
    /// Let a container be as tall as the rows in it, so a section grows with
    /// its content instead of clipping at whatever height the donor had.
    /// </summary>
    public static void FitHeight(RectTransform rect)
    {
        ContentSizeFitter fitter = rect.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        }

        // Switched on explicitly: a copy may arrive with one of the donor's own,
        // and ContentSized turns those off.
        fitter.enabled = true;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// Turn <paramref name="window"/> into a window onto <paramref name="content"/>
    /// that can be scrolled: the content as tall as what is in it, the window
    /// clipping what does not fit, and a <see cref="ScrollRect"/> moving one
    /// behind the other. Null when the window will not take one, which is the
    /// caller's to report.
    /// </summary>
    /// <param name="like">
    /// A scroll view of the game's own to take the feel from — how far a notch of
    /// the wheel moves, whether the content keeps sliding — so the panel scrolls
    /// like the list beside it rather than like something a mod built. Null is
    /// ordinary; Unity's defaults then stand.
    /// </param>
    /// <remarks>
    /// <para>
    /// The content is anchored to the top of the window and left to size itself.
    /// A rect stretched to its parent can never be taller than the parent, and a
    /// content that is never taller than its window is a scroll view that never
    /// scrolls — which is the state the detail panel was in when its last row was
    /// cut off by the panel edge.
    /// </para>
    /// <para>
    /// The clip is a <see cref="RectMask2D"/> and the surface that catches the
    /// wheel is an <see cref="Image"/> with no colour at all. Neither needs a
    /// sprite, a material or a font, so this ships no asset.
    /// </para>
    /// </remarks>
    public static ScrollRect MakeScrollView(RectTransform window, RectTransform content, ScrollRect like)
    {
        if (window == null || content == null)
        {
            return null;
        }

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
        content.anchoredPosition = Vector2.zero;
        FitHeight(content);

        if (window.GetComponent<RectMask2D>() == null)
        {
            window.gameObject.AddComponent<RectMask2D>();
        }

        Image surface = window.GetComponent<Image>();
        if (surface == null)
        {
            surface = window.gameObject.AddComponent<Image>();
        }

        if (surface != null)
        {
            // Invisible, and still a raycast target: without a graphic under the
            // pointer the wheel reaches nothing and the panel does not scroll.
            surface.sprite = null;
            surface.color = new Color(0f, 0f, 0f, 0f);
            surface.raycastTarget = true;
        }

        ScrollRect scroll = window.GetComponent<ScrollRect>();
        if (scroll == null)
        {
            scroll = window.gameObject.AddComponent<ScrollRect>();
        }

        if (scroll == null)
        {
            return null;
        }

        scroll.content = content;
        scroll.viewport = window;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbar = null;

        // Clamped unless the game's own view says otherwise: a panel of figures
        // that springs back past its own last line reads as a toy, and this is
        // the one setting that is ours rather than the donor's when there is no
        // donor to ask.
        scroll.movementType = ScrollRect.MovementType.Clamped;

        if (like != null)
        {
            scroll.movementType = like.movementType;
            scroll.elasticity = like.elasticity;
            scroll.scrollSensitivity = like.scrollSensitivity;
            scroll.inertia = like.inertia;
            scroll.decelerationRate = like.decelerationRate;
        }

        return scroll;
    }

    /// <summary>
    /// Point a button at one of our own methods: managed listeners cleared,
    /// listeners baked into the prefab switched off. <see cref="UnityEvent"/>
    /// keeps the serialized ones through <c>RemoveAllListeners</c>, and those
    /// still name the component we are about to remove.
    /// </summary>
    public static void Rewire(Button button, Action handler)
    {
        if (button == null)
        {
            return;
        }

        Button.ButtonClickedEvent clicked = button.onClick;
        for (int i = 0; i < clicked.GetPersistentEventCount(); i++)
        {
            clicked.SetPersistentListenerState(i, UnityEventCallState.Off);
        }

        clicked.RemoveAllListeners();
        clicked.AddListener(handler);
    }

    /// <summary>
    /// The same for a field the player may type into: point its committed entry
    /// at one of our own methods and silence everything the prefab brought.
    /// </summary>
    /// <remarks>
    /// <b>Both events, and the serialized ones above all.</b> The donor is the
    /// product panel's price field, and the listeners baked into it write a
    /// price. They have never fired on our copy because the copy was read-only;
    /// a field the player can type into would fire them, and a settings page
    /// would be re-pricing a product. <c>onValueChanged</c> is cleared as well
    /// as <c>onEndEdit</c> because the donor's runs on every keystroke.
    /// </remarks>
    public static void Rewire(InputField field, Action<string> committed)
    {
        if (field == null)
        {
            return;
        }

        Silence(field.onValueChanged);

        InputField.EndEditEvent ended = field.onEndEdit;
        Silence(ended);
        ended.AddListener(committed);
    }

    /// <summary>
    /// Everything a prefab brought with an event, switched off: the managed
    /// listeners removed and the serialized ones turned off, which
    /// <c>RemoveAllListeners</c> leaves behind.
    /// </summary>
    private static void Silence(UnityEventBase listeners)
    {
        if (listeners == null)
        {
            return;
        }

        for (int i = 0; i < listeners.GetPersistentEventCount(); i++)
        {
            listeners.SetPersistentListenerState(i, UnityEventCallState.Off);
        }

        listeners.RemoveAllListeners();
    }

    /// <summary>
    /// Remove every child of a transform. They are deactivated first, because
    /// <c>Destroy</c> only takes effect at the end of the frame and a layout
    /// group would otherwise spend that frame making room for the departing
    /// children as well as the arriving ones.
    /// </summary>
    public static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }

    public static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    public static void SetActive(Component target, bool active)
    {
        if (target != null)
        {
            target.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// Every <see cref="Text"/> under <paramref name="root"/> that is not inside
    /// one of the excluded subtrees, in hierarchy order. This is how a section's
    /// header label and a row's own labels are found without knowing the
    /// hierarchy, and how labels belonging to switched-off controls are kept out
    /// of the way.
    /// </summary>
    public static Text[] TextsOutside(GameObject root, params Component[] excluded)
    {
        Il2CppArrayBase<Text> found = root.GetComponentsInChildren<Text>(true);
        var kept = new List<Text>(found.Length);

        foreach (Text candidate in found)
        {
            bool inside = false;
            foreach (Component subtree in excluded)
            {
                if (subtree != null && candidate.transform.IsChildOf(subtree.transform))
                {
                    inside = true;
                    break;
                }
            }

            if (!inside)
            {
                kept.Add(candidate);
            }
        }

        return kept.ToArray();
    }

    /// <summary>The first of <see cref="TextsOutside"/>, or null.</summary>
    public static Text FindLabelOutside(GameObject root, params Component[] excluded)
    {
        Text[] candidates = TextsOutside(root, excluded);
        return candidates.Length > 0 ? candidates[0] : null;
    }

    /// <summary>
    /// Make a container stack its children top to bottom, each as tall as it says
    /// it needs to be. Answers whether it does now — which is false only when it
    /// would take no stack at all, and is then the caller's to report.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>childControlHeight</c> is the line that decides whether the stack works
    /// at all. With it off, <c>VerticalLayoutGroup</c> takes each child's height
    /// from its <c>sizeDelta</c> — which for a stretched child is nearly zero, and
    /// for a child sized by a <see cref="ContentSizeFitter"/> is whatever it was
    /// before the fitter last ran. That is how the first build came to draw a
    /// wrapped paragraph and the block after it in the same place. With it on, the
    /// group asks each child what height it needs and gives it that.
    /// </para>
    /// <para>
    /// So it is set whether the group is one we added or one the copied container
    /// brought with it. The earlier version answered "yes, it lays its children
    /// out" for <em>any</em> layout group already attached — including a grid,
    /// which would put our rows side by side, and including a vertical group with
    /// <c>childControlHeight</c> off, which stacks rows by a size nothing set.
    /// Both would have been reported as a stack in place.
    /// </para>
    /// <para>
    /// A group that was already there keeps its own spacing, padding and
    /// alignment: those are the panel's look, and this is only here to make the
    /// stack work.
    /// </para>
    /// </remarks>
    public static bool EnsureVerticalLayout(GameObject container, float spacing, int padding)
    {
        if (container == null)
        {
            return false;
        }

        var rect = container.GetComponent<RectTransform>();
        if (rect == null)
        {
            return false;
        }

        // Anything that is not a vertical stack would lay the rows out some other
        // way, and Unity refuses a second LayoutGroup while one is attached.
        ClearForeignLayouts(rect);

        VerticalLayoutGroup existing = container.GetComponent<VerticalLayoutGroup>();
        VerticalLayoutGroup layout = existing != null ? existing : Stack(container);
        if (layout == null)
        {
            return false;
        }

        if (existing == null)
        {
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.padding = new RectOffset(padding, padding, padding, padding);
        }

        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        return true;
    }

    /// <summary>
    /// The one vertical stack on this object: the one it already has, or one put
    /// there now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null is a real answer, and the caller has to read it. <c>LayoutGroup</c> is
    /// declared <c>[DisallowMultipleComponent]</c>, so <c>AddComponent</c> refuses
    /// — and returns null — whenever another layout group is still attached.
    /// <em>Still</em> is the word: <c>Destroy</c> only takes the component off at
    /// the end of the frame, so a group destroyed a line earlier is every bit as
    /// much in the way as one nobody touched. Dereferencing what came back is
    /// what emptied the app.
    /// </para>
    /// </remarks>
    public static VerticalLayoutGroup Stack(GameObject host)
    {
        if (host == null)
        {
            return null;
        }

        VerticalLayoutGroup existing = host.GetComponent<VerticalLayoutGroup>();
        return existing != null ? existing : host.AddComponent<VerticalLayoutGroup>();
    }

    /// <summary>
    /// Take every layout group off this container except a vertical one.
    /// </summary>
    /// <remarks>
    /// <c>DestroyImmediate</c>, not <c>Destroy</c>, and that is the whole fix for
    /// an app that opened empty. <c>Destroy</c> takes the component off at the end
    /// of the frame; until then it is still attached, and <c>LayoutGroup</c> is
    /// <c>[DisallowMultipleComponent]</c>, so the vertical group that should
    /// replace it cannot be added — <c>AddComponent</c> answers null and the next
    /// line throws. Taking the grid off now also spares the list the frame of grid
    /// widths it would otherwise be measured against.
    /// </remarks>
    private static void ClearForeignLayouts(RectTransform container)
    {
        foreach (LayoutGroup group in container.GetComponents<LayoutGroup>())
        {
            if (group == null || group.TryCast<VerticalLayoutGroup>() != null)
            {
                continue;
            }

            group.enabled = false;
            Object.DestroyImmediate(group);
        }
    }

    /// <summary>
    /// Make a cloned tile behave as a row: as wide as the list and exactly
    /// <paramref name="height"/> tall, whatever size the tile itself was.
    /// </summary>
    public static void MakeRow(GameObject row, float height)
    {
        LayoutElement element = row.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = row.AddComponent<LayoutElement>();
        }

        element.ignoreLayout = false;
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleWidth = 1f;
    }

    /// <summary>
    /// Stretch one of a tile's own parts over the whole row.
    /// </summary>
    /// <remarks>
    /// A product tile's frame, its selection outline and its button are all sized
    /// for a square. Left alone in a row they read as a small box in the middle of
    /// a wide line — and in the button's case the area that answers a click is a
    /// fraction of the area that looks clickable. The part itself is the game's
    /// own, sprite and colour and all; only its rect changes. A part that <em>is</em>
    /// the row is left alone: its size is the list's business.
    /// </remarks>
    public static void StretchOver(Component part, Transform row)
    {
        if (part == null || row == null || part.transform.Pointer == row.Pointer)
        {
            return;
        }

        RectTransform rect = part.GetComponent<RectTransform>();
        if (rect != null)
        {
            Fill(rect);
        }
    }

    /// <summary>
    /// Make a tile's own backing parts cover their row exactly and sit behind
    /// everything the row writes, ordered back to front as they are given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things go wrong otherwise, and the selection highlight suffered both.
    /// A part nested inside one of the tile's inner boxes is stretched over
    /// <em>that</em> box, which in a wide row is a bar of some length other than
    /// the row's — so each part is moved up to the row first and then filled, and
    /// can then only end where the row ends.
    /// </para>
    /// <para>
    /// And a part the donor happened to order after its labels draws over them: a
    /// green bar across the money on the selected line. Sibling order is draw
    /// order in a canvas, so these take the first indices and the row's labels are
    /// put last.
    /// </para>
    /// </remarks>
    public static void PlaceBehind(Transform row, params Component[] parts)
    {
        if (row == null)
        {
            return;
        }

        int index = 0;
        foreach (Component part in parts)
        {
            if (part == null || part.transform.Pointer == row.Pointer)
            {
                continue;
            }

            RectTransform rect = part.GetComponent<RectTransform>();
            if (rect == null)
            {
                continue;
            }

            rect.SetParent(row, false);
            Fill(rect);
            rect.SetSiblingIndex(index);
            index++;
        }
    }

    /// <summary>
    /// Put a row's name and its figure in two columns that cannot reach each
    /// other: the figure keeps the right-hand end at exactly the width it needs,
    /// and the name has everything to the left of it.
    /// </summary>
    /// <remarks>
    /// The first build stretched both labels across the whole tile and let them
    /// draw over one another — "Sam Thomp $210.50". Anchoring the figure to the
    /// right at its own preferred width is what makes the overlap impossible
    /// rather than unlikely.
    /// </remarks>
    public static void PlaceColumns(Text name, Text figure, float pad, float gap)
    {
        float figureWidth = figure != null ? Mathf.Ceil(figure.preferredWidth) : 0f;

        if (figure != null)
        {
            RectTransform rect = figure.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(figureWidth, 0f);
            rect.anchoredPosition = new Vector2(-pad, 0f);
            figure.alignment = TextAnchor.MiddleRight;
            figure.horizontalOverflow = HorizontalWrapMode.Overflow;
            figure.verticalOverflow = VerticalWrapMode.Truncate;
        }

        if (name == null)
        {
            return;
        }

        RectTransform left = name.rectTransform;
        left.anchorMin = new Vector2(0f, 0f);
        left.anchorMax = new Vector2(1f, 1f);
        left.pivot = new Vector2(0.5f, 0.5f);
        left.offsetMin = new Vector2(pad, 0f);
        left.offsetMax = new Vector2(-(figureWidth + gap + pad), 0f);
        name.alignment = TextAnchor.MiddleLeft;
        name.horizontalOverflow = HorizontalWrapMode.Wrap;
        name.verticalOverflow = VerticalWrapMode.Truncate;
    }

    /// <summary>
    /// The ancestor of <paramref name="node"/> that is a direct child of
    /// <paramref name="root"/>, or null when it is not under the root at all.
    /// </summary>
    /// <remarks>
    /// Which branch of the screen something is in. Two widgets that share a
    /// branch are moved by moving the branch once; two that do not have to be
    /// moved apart — and telling those cases apart is what this is for, because
    /// moving one branch twice would move it twice as far.
    /// </remarks>
    public static RectTransform BranchUnder(Transform root, Transform node)
    {
        if (root == null || node == null)
        {
            return null;
        }

        for (Transform step = node; step != null; step = step.parent)
        {
            if (step.parent != null && step.parent.Pointer == root.Pointer)
            {
                return step as RectTransform;
            }
        }

        return null;
    }

    /// <summary>
    /// How far the top edge of <paramref name="rect"/> is below the top edge of
    /// <paramref name="frame"/>, in the frame's own space. NaN where either has
    /// not been laid out yet, which <see cref="Measurement.IsReal"/> answers.
    /// </summary>
    /// <remarks>
    /// Through world space and back, so the answer holds however many objects
    /// are between the two and whatever each of them does with its anchors or
    /// its scale. A rect's own <c>rect</c> is in its local space with its pivot
    /// already accounted for, which is why an edge is read off it rather than
    /// worked out from a position and a height.
    /// </remarks>
    public static float TopBelow(RectTransform rect, RectTransform frame)
    {
        if (rect == null || frame == null)
        {
            return float.NaN;
        }

        // The frame's own top edge is already in the frame's space; only the
        // other one has to be brought into it.
        float edge = frame.InverseTransformPoint(rect.TransformPoint(new Vector3(0f, rect.rect.yMax, 0f))).y;
        return frame.rect.yMax - edge;
    }

    /// <summary>Stretch a rect to fill its parent, inset by <paramref name="inset"/>.</summary>
    public static void Fill(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>
    /// Exempt a rect from any layout group its parent may have, so the anchors
    /// we set are the ones that apply.
    /// </summary>
    public static void IgnoreLayout(RectTransform rect)
    {
        LayoutElement element = rect.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = rect.gameObject.AddComponent<LayoutElement>();
        }

        element.ignoreLayout = true;
    }

}
