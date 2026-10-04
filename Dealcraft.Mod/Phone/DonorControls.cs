using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dealcraft.Phone;

/// <summary>
/// The controls the cloned <c>ProductAppDetailPanel</c> already carries, kept
/// so the settings page can be drawn out of them.
/// </summary>
/// <remarks>
/// <para>
/// They are read in <see cref="AppLayout.TryRead"/>, <b>before</b>
/// <c>AppShellView</c> clears the app's content — which is the whole reason this
/// type exists. The panel's own component is disabled and its container hidden a
/// moment later, and a handle taken after that is a handle to something the
/// donor has already put away.
/// </para>
/// <para>
/// Every one of them is on the component the app already clones; they are simply
/// not read today. No widget here is built, styled or shipped: a switch is the
/// panel's <c>ListedForSale</c> toggle, a stepper is its <c>ValueLabel</c> input
/// field between its own <c>−</c> and <c>+</c>, and a bar is its
/// <c>AddictionSlider</c>.
/// </para>
/// </remarks>
internal sealed class DonorControls
{
    /// <summary>
    /// The panel's own tick box, from <c>ListedForSale</c> — or
    /// <c>FavouriteProduct</c> where a build has only that one.
    /// </summary>
    public Toggle Toggle;

    /// <summary>The panel's editable number, from <c>ValueLabel</c>.</summary>
    public InputField Value;

    /// <summary>The <c>+</c> beside it, from <c>_addButton</c>.</summary>
    public Transform Add;

    /// <summary>The <c>−</c> beside it, from <c>_removeButton</c>.</summary>
    public Transform Remove;

    /// <summary>
    /// The panel's own bar, from <c>AddictionSlider</c>. A <c>Scrollbar</c> the
    /// game only ever reads.
    /// </summary>
    /// <remarks>
    /// Nothing draws it today. It is kept because the handle is the expensive
    /// half — it is read here, before <c>AppPageView</c> clears the app's
    /// content, and cannot be taken afterwards.
    /// <para>
    /// A recipe for turning a copy of it into a read-only fill did exist, in
    /// <c>DetailStrip.FromScrollbar</c> in <c>Phone/Gauges.cs</c>, and ticket 38
    /// deleted it with the two tabs that were its only callers. It was four
    /// lines of configuration — <c>transition</c>, <c>interactable</c>,
    /// <c>navigation</c> and <c>direction</c> off or fixed, the prefab's own
    /// listeners switched off, then <c>value = 0</c> and <c>size</c> as the
    /// fill — and it is in the history rather than in the tree because a
    /// <em>read-only</em> bar is the opposite of the draggable control
    /// <c>app.md</c> asks for next.
    /// </para>
    /// </remarks>
    public Scrollbar Bar;

    /// <summary>Whether a stepper can be built out of these at all.</summary>
    public bool CanStep => Value != null && Add != null && Remove != null;

    /// <summary>Whether every control the page draws was found.</summary>
    /// <remarks>
    /// <b><see cref="Bar"/> is deliberately not among them.</b> It was, and that
    /// made this false on a build where the page would in fact have drawn
    /// perfectly: nothing draws the bar, and after ticket 38 discarded
    /// <c>DetailStrip.FromScrollbar</c> nothing can. A build whose detail panel
    /// lends no <c>AddictionSlider</c> got warned about a degradation that cannot
    /// happen. The handle is still taken — see the remarks on the field for why
    /// it cannot be taken later — it just does not decide whether the page is
    /// whole.
    /// </remarks>
    public bool Complete => Toggle != null && CanStep;

    /// <summary>
    /// What was not there, named for the log. A build this list does not
    /// recognise is worth saying out loud, because the page then falls back to
    /// the list row and looks less like the game than it should.
    /// </summary>
    public string Missing
    {
        get
        {
            var absent = new List<string>(2);
            if (Toggle == null)
            {
                absent.Add("no toggle");
            }

            if (Value == null || Add == null || Remove == null)
            {
                absent.Add("no stepper");
            }

            // The bar is not asked about. Nothing draws it, so its absence is
            // not a degradation and saying so was a false alarm.
            return absent.Count == 0 ? "everything the app asked for" : string.Join(", ", absent);
        }
    }
}
