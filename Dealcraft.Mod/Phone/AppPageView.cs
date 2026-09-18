using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using UnityEngine;
using UnityEngine.UI;

namespace Dealcraft.Phone;

/// <summary>
/// Dealcraft's page inside the cloned Product Manager layout: the donor's own
/// content taken down, and the settings form put over what is left. One page —
/// there is no list column, no detail panel and no tab strip to choose between
/// them.
/// </summary>
/// <remarks>
/// <para>
/// This is what is left of <c>AppShellView</c>, which drew a master list on the
/// left and a detail panel on the right and hung a cloned tab strip across the
/// top of both. The owner will never open either list — <i>"Reiter 1 und 2
/// brauche ich erst einmal gar nicht"</i> — and the game already has a customer
/// list and a product list of its own.
/// </para>
/// <para>
/// Nothing here decides what the app says. <see cref="AutomationForm"/> decides
/// that from a reading of the preferences file, and this puts the answer on
/// screen and hands a press back.
/// </para>
/// </remarks>
internal sealed class AppPageView
{
    private readonly AppLayout _layout;
    private readonly Func<IReadOnlyList<AutomationSetting>> _automation;

    /// <summary>
    /// Whose machine this is, asked again with every reading of the settings.
    /// The mod's own reader answers it, the same one every pass asks before it
    /// acts, so the page and the passes cannot disagree about who the host is.
    /// </summary>
    private readonly Func<ServerAuthority> _authority;

    private readonly Func<SettingChange, bool> _write;
    private readonly Action<string> _log;
    private readonly Action<string> _warn;

    /// <summary>
    /// The placement is retried until the app has been laid out, so the same
    /// sentence would otherwise be written four times a second.
    /// </summary>
    private readonly SaidOnce _said = new();

    /// <summary>The page itself: a form, drawn out of the donor's own controls.</summary>
    private AutomationView _automationView;

    /// <summary>
    /// The form as the preferences file last read. Asked for again after every
    /// change rather than the new value being remembered, so what the app shows
    /// is always a reading of the file.
    /// </summary>
    private AutomationForm _form;

    public AppPageView(
        AppLayout layout,
        Func<IReadOnlyList<AutomationSetting>> automation,
        Func<ServerAuthority> authority,
        Func<SettingChange, bool> write,
        Action<string> log,
        Action<string> warn)
    {
        _layout = layout;
        _automation = automation;
        _authority = authority;
        _write = write;
        _log = log;
        _warn = warn;
    }

    /// <summary>
    /// What this screen was still waiting for at the last pass, in the words the
    /// log should use if the waiting runs out. Empty once everything has settled.
    /// </summary>
    public string Unsettled { get; private set; } = string.Empty;

    public void Build()
    {
        int cleared = EmptyDonorContent();

        BuildAutomation();
        HideDonorContent();

        _log($"app content built after clearing {cleared} product row(s) the copy had built "
            + "for itself; the app opens on its settings page");

        // Last, and its answer is the caller's to wait on: at install the app is
        // closed and nothing on this screen has a size, which is a state of the
        // scene rather than a page at the top of the app.
        Settle();
    }

    /// <summary>
    /// Put our own state back on screen. The cloned app still runs the vanilla
    /// Product Manager's code, and that code shows its own detail panel when the
    /// app opens, so this is re-applied every time the app comes up rather than
    /// only once at install.
    /// </summary>
    public void Reassert()
    {
        // The copy stays subscribed to the game's product events for as long as
        // the scene lives, and those handlers build rows. Anything that has
        // arrived since the last open is cleared before the app is shown again.
        EmptyDonorContent();
        HideDonorContent();

        // The preferences file may have been edited by hand, or by another
        // player's client, while the app was shut. It is the only store, so the
        // page is a reading of it taken now rather than the one taken when the
        // app was installed. Whose machine this is is read again with it: an
        // install that happened before the session was up would otherwise leave
        // the page saying so for as long as the scene lives.
        RepaintAutomation();

        _automationView?.ScrollToTop();
    }

    /// <summary>
    /// One pass at putting the page where it belongs, and whether there is
    /// anything left to wait for: <see cref="SceneLook.NotReady"/> while the app
    /// has not been laid out, which is what the caller asks again.
    /// </summary>
    /// <remarks>
    /// Public because the waiting is the caller's. This view has no frames of
    /// its own — it is built inside one — and the sizes it needs are produced by
    /// the engine on some later one.
    /// </remarks>
    public SceneLook Settle()
    {
        if (_automationView == null)
        {
            return SceneLook.Settled;
        }

        RectTransform frame = _layout.AppContainer;
        List<RectTransform> branches = ContentBranches(frame);

        if (frame == null || branches.Count == 0)
        {
            // Nothing to measure against, and no later look would change that.
            //
            // The owner met exactly this on 2026-09-18: the page ran up into the
            // map's heading. The line said the branches were missing and stopped
            // there, and because Say is deduplicated by (subject, state) it was
            // said once at install and never again — so there was no way to tell
            // whether it still failed with the app open, nor which of the three
            // handles was the missing one. It now says both.
            Say("page-top", "no-frame", "the app has no content area to measure, so the settings "
                + "page starts at the very top of the app and may cover its header band"
                + $" — app container {(frame == null ? "missing" : "found")}"
                + $", list scroll {(_layout.ListScroll == null ? "missing" : "found")}"
                + $", section parent {(_layout.SectionParent == null ? "missing" : "found")}"
                + $", detail container {(_layout.DetailContainer == null ? "missing" : "found")}"
                + $", and the app is {(frame != null && frame.gameObject.activeInHierarchy ? "on screen" : "not on screen")}");
            return SceneLook.Settled;
        }

        if (!Measurement.IsReal(frame.rect.height))
        {
            Unsettled = "the app never reported a size while it was open, so the settings page "
                + "may sit over the header band rather than below it";
            return SceneLook.NotReady;
        }

        // The highest branch is the one the header band ends above. Nothing here
        // knows how tall that band is: what is measured is the top edge of the
        // app's own content against the top edge of the app, and the band is
        // whatever is above it. So a patch that changes the header changes this
        // with it.
        float screenTop = float.MaxValue;
        foreach (RectTransform branch in branches)
        {
            // A branch with no height is a branch the engine has not laid out,
            // and its top edge reads as the top of everything. Waiting for one is
            // the difference between measuring the header and measuring nothing.
            if (!Measurement.IsReal(branch.rect.height))
            {
                Unsettled = "the app never reported a size while it was open, so the settings "
                    + "page may sit over the header band rather than below it";
                return SceneLook.NotReady;
            }

            float top = Widgets.TopBelow(branch, frame);
            if (!Measurement.IsFinite(top))
            {
                Unsettled = "the app never reported a size while it was open, so the settings "
                    + "page may sit over the header band rather than below it";
                return SceneLook.NotReady;
            }

            screenTop = Mathf.Min(screenTop, top);
        }

        _automationView.StartBelow(screenTop < 0f ? 0f : screenTop);
        Unsettled = string.Empty;
        return SceneLook.Settled;
    }

    /// <summary>
    /// The branches of the app's screen the page has to start below: the one the
    /// donor's list is in, and the one its detail panel is in.
    /// </summary>
    /// <remarks>
    /// Both, and de-duplicated, because either may be the one the header band
    /// ends above and the two may share one container. The donor's own content
    /// inside them is hidden rather than destroyed — its component keeps running
    /// and would dereference it — so the branches are still there to measure.
    /// </remarks>
    private List<RectTransform> ContentBranches(RectTransform frame)
    {
        var branches = new List<RectTransform>(2);
        if (frame == null)
        {
            return branches;
        }

        Transform list = _layout.ListScroll != null
            ? _layout.ListScroll.transform
            : _layout.SectionParent;

        Transform detail = _layout.DetailContainer != null
            ? _layout.DetailContainer.transform.parent
            : null;

        Add(branches, Widgets.BranchUnder(frame, list));
        Add(branches, Widgets.BranchUnder(frame, detail));

        return branches;
    }

    private static void Add(List<RectTransform> branches, RectTransform branch)
    {
        if (branch == null)
        {
            return;
        }

        foreach (RectTransform known in branches)
        {
            if (known.Pointer == branch.Pointer)
            {
                return;
            }
        }

        branches.Add(branch);
    }

    /// <summary>
    /// Take out the product list the copy built for itself, and say how many
    /// rows that was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A freshly cloned app is not empty. <c>ProductManagerApp.Start</c> walks
    /// the game's static product registries and calls <c>CreateEntry</c> and
    /// <c>CreateFavouriteEntry</c> with <c>this</c> as the receiver — read out
    /// of the machine code, see <c>docs/native-truth.md</c> — so the copy ends
    /// up holding one row per discovered product and one per favourite, in its
    /// own containers and its own <c>entries</c> list. The vanilla app is
    /// untouched; the second list is ours.
    /// </para>
    /// <para>
    /// Deactivating the containers is not enough. <c>SetOpen</c> walks
    /// <c>entries</c> on every open and calls <c>UpdateDiscovered</c> and
    /// <c>UpdateListed</c> on each row, and any of the app's own code that
    /// reactivates a container would put vanilla products back on Dealcraft's
    /// screen. So the rows go, and emptying the two lists is what makes that
    /// safe: <c>SetOpen</c>, <c>OnProductListedEvent</c> and
    /// <c>RemoveFavouriteEntry</c> are the only things that walk them, and an
    /// empty list gives them nothing to dereference.
    /// </para>
    /// <para>
    /// <c>ProductEntry.Destroy</c> is the game's own method for this: it hides
    /// the row and destroys its object, and the resulting <c>OnDestroy</c>
    /// unsubscribes the row from the product events it registered for. Removing
    /// them by hand would leave those subscriptions behind.
    /// </para>
    /// </remarks>
    private int EmptyDonorContent()
    {
        ProductManagerApp app = _layout.App;
        if (app == null)
        {
            return 0;
        }

        return Empty(app.entries) + Empty(app.favouriteEntries);
    }

    private static int Empty(Il2CppSystem.Collections.Generic.List<ProductEntry> rows)
    {
        if (rows is null)
        {
            return 0;
        }

        int removed = 0;
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            ProductEntry row = rows[i];
            if (row == null)
            {
                continue;
            }

            row.Destroy();
            removed++;
        }

        rows.Clear();
        return removed;
    }

    /// <summary>
    /// Put the donor's own list, its detail panel and its "nothing selected"
    /// line out of sight. They are deactivated rather than destroyed: the
    /// donor's component keeps running and would dereference them.
    /// </summary>
    /// <remarks>
    /// The empty-state line goes with them. It reads "Select a product to view
    /// details." and there is nothing on this page to select, so leaving it up
    /// would be the app asking for something it cannot be given.
    /// </remarks>
    private void HideDonorContent()
    {
        foreach (ProductTypeContainer section in _layout.OwnSections)
        {
            Widgets.SetActive(section, false);
        }

        Widgets.SetActive(_layout.DetailContainer, false);
        Widgets.SetActive(_layout.NothingSelected, false);

        // Its Update would otherwise keep rewriting the labels of the product
        // panel we just hid.
        _layout.Detail.enabled = false;
    }

    /// <summary>
    /// Build the settings page and draw what the preferences file currently says
    /// into it.
    /// </summary>
    private void BuildAutomation()
    {
        try
        {
            _form = AutomationForm.Build(_automation(), _authority());

            var view = new AutomationView(
                _layout, PressSetting, StepSetting, TypeSetting, _log, _warn);
            view.Build(
                _layout.AppContainer,
                _layout.ListScroll != null ? _layout.ListScroll : _layout.NearestScroll);

            if (!view.IsBuilt)
            {
                return;
            }

            _automationView = view;
            _automationView.Paint(_form);
        }
        catch (Exception error)
        {
            _warn($"the settings page could not be built, so the app's settings can only be "
                + $"changed in MelonPreferences.cfg: {error.Message}");
        }
    }

    /// <summary>
    /// A player pressed a control. The form says what to write, the write goes
    /// to whoever owns the entry, and the page is drawn again from a fresh
    /// reading of the file.
    /// </summary>
    private bool PressSetting(string rowId) =>
        _form is not null && Wrote(_form.Press(rowId));

    /// <summary>
    /// One end of a stepper was pressed. The same path, with the direction: the
    /// form answers with nothing at an end its control does not go past, so
    /// nothing is written and the page is not redrawn.
    /// </summary>
    private bool StepSetting(string rowId, bool up) =>
        _form is not null && Wrote(_form.Step(rowId, up));

    /// <summary>
    /// Something was typed into a field and committed. The form reads it
    /// against what that row will take and answers with a write or with
    /// nothing; nothing is the entry refused, and the view puts the old value
    /// back rather than clamping it to the nearest end.
    /// </summary>
    private bool TypeSetting(string rowId, string text) =>
        _form is not null && Wrote(_form.Type(rowId, text));

    /// <summary>
    /// Write what a control asked for and redraw the page from what the file
    /// then says. A write the owner refused leaves the control showing the old
    /// value, which is the truth, instead of a new one nothing holds.
    /// </summary>
    /// <remarks>
    /// A list, because a control may step a pair of entries together. They are
    /// written before anything is redrawn, so the page is never painted from a
    /// file holding half a rung.
    /// </remarks>
    private bool Wrote(IReadOnlyList<SettingChange> changes)
    {
        if (changes.Count == 0)
        {
            return false;
        }

        var written = new List<SettingChange>(changes.Count);
        foreach (SettingChange change in changes)
        {
            if (_write(change))
            {
                written.Add(change);
                continue;
            }

            _warn($"nothing owns the setting '{change.Key}', so it was not changed");
        }

        if (written.Count == 0)
        {
            return false;
        }

        RepaintAutomation();

        foreach (SettingChange change in written)
        {
            // What the file says now, not what was asked for. An owner that held
            // the value inside its own bounds would otherwise be reported as
            // having taken a figure it did not.
            _log($"{change.Key} is now {_form.ValueOf(change.Key)}");
        }

        return true;
    }

    /// <summary>
    /// Read the preferences file again and draw the page from it.
    /// </summary>
    /// <remarks>
    /// Guarded because one of its two callers is the game's update loop, by way
    /// of the app being opened, and the other is a click handler Il2Cpp invokes
    /// out of the game's own button dispatch. Neither is ours to throw into.
    /// </remarks>
    private void RepaintAutomation()
    {
        if (_automationView == null)
        {
            return;
        }

        try
        {
            _form = AutomationForm.Build(_automation(), _authority());
            _automationView.Paint(_form);
        }
        catch (Exception error)
        {
            _warn($"the settings page could not be drawn again, so it may be showing what the "
                + $"preferences file said a moment ago: {error.Message}");
        }
    }

    /// <summary>
    /// Say it the first time, and again only when what there is to say about
    /// that same subject has changed kind.
    /// </summary>
    private void Say(string subject, string state, string message)
    {
        if (_said.ShouldSay(subject, state))
        {
            _log(message);
        }
    }
}
