using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using MelonLoader;

namespace Dealcraft;

/// <summary>
/// Accepts standing offers into the deal windows the host allows. Host only,
/// and inert on a fresh install because no window is allowed on one.
/// </summary>
/// <remarks>
/// The settings that are only this feature's — which windows are allowed, and
/// when a window is crowded enough to be worth mentioning — are declared and
/// described here rather than in <see cref="ModSettings"/>. They still live in
/// the mod's one MelonPreferences category, which <see cref="ModSettings"/>
/// owns; this only says what they are.
/// </remarks>
internal sealed class DealSchedulingFeature : Feature
{
    /// <summary>
    /// One entry per deal window, keyed by the same name the app's row is keyed
    /// by, so a value edited in the file and a row read in the app are
    /// demonstrably the same setting. The file is the only store.
    /// </summary>
    private readonly Dictionary<DealWindow, MelonPreferences_Entry<bool>> _allowedWindows = new();

    private FeatureContext _context;
    private DealSchedulingLoop _scheduling;

    public override string Name => "schedule";

    public override void Declare(MelonPreferences_Category category)
    {
        // Which windows scheduling may use. Each is on or off on its own, and
        // all four off means no auto-scheduling at all — a real answer, and the
        // one a fresh install gets.
        //
        // All four default to false, and that is a fix rather than a default
        // somebody picked. Late Night used to ship ticked, because that is the
        // window the original mod applied to every deal, and it was harmless
        // only while a parent switch was off above it. This page deletes that
        // switch: four ticked boxes already say which windows are allowed, and
        // a switch above them could only contradict them. With Late Night ticked
        // a fresh install
        // would start scheduling into the night the moment it loaded, against
        // the spec's own first promise that installing the mod changes nothing.
        foreach (DealWindow window in DealWindowSet.All)
        {
            _allowedWindows[window] = category.CreateEntry(
                DealWindowCatalog.KeyOf(window),
                false,
                description: $"Host only: allow deals to be scheduled into the {DealWindowName.Of(window)} "
                    + "window. Its real clock times are shown in the Dealcraft app and in the log. "
                    + "All four are off on a fresh install, so nothing is scheduled until one is "
                    + "switched on.");
        }
    }

    public override IReadOnlyList<AutomationSetting> Describe() => DealWindowCatalog.Describe(
        AllowedWindows(),
        _scheduling?.WindowHours() ?? Array.Empty<DealWindowHours>());

    public override bool Change(SettingChange change)
    {
        foreach (MelonPreferences_Entry<bool> window in _allowedWindows.Values)
        {
            if (Preference.Set(change, window))
            {
                return true;
            }
        }

        return false;
    }

    public override void Start(FeatureContext context)
    {
        _context = context;

        // Its own claim registry, not one shared with the handover. Both key a
        // contract, but not with the same string: this names an offer by the
        // NPC and the moment it arrived, because the contract does not exist
        // yet, while the handover names a live contract by its GUID. A shared
        // registry would be reconciled against one key space at a time, and so
        // would keep forgetting the other feature's claims.
        _scheduling = new DealSchedulingLoop(
            new ContractClaimRegistry(),
            context.Lifecycle(),
            Configuration,
            context.Log(Name),
            context.Warn(Name));
    }

    /// <summary>The scheduler caches scene objects, and drops them here.</summary>
    public override void SceneChanged() => _scheduling?.SceneChanged();

    public override void Tick() => _scheduling?.Tick();

    /// <summary>
    /// The switch itself is one of the shared settings and is reported with
    /// them, so only the windows are this feature's to say.
    /// </summary>
    public override string Summary() => $"Deal windows allowed: {AllowedWindows()}.";

    /// <summary>
    /// Which windows the host allows, read afresh so a hand-edited preferences
    /// file is picked up without a restart. The file stays the only store.
    /// </summary>
    private DealWindowSet AllowedWindows()
    {
        var windows = new DealWindowSet();
        foreach (KeyValuePair<DealWindow, MelonPreferences_Entry<bool>> entry in _allowedWindows)
        {
            windows.Allow(entry.Key, entry.Value.Value);
        }

        return windows;
    }

    /// <summary>
    /// Everything a scheduling sweep needs, read fresh each sweep so that a
    /// hand-edited preferences file takes effect without a restart.
    /// </summary>
    private SchedulingConfiguration Configuration()
    {
        DealWindowSet allowed = AllowedWindows();

        // There is no switch above the four windows any more. "Enabled" is
        // whether the host allows any window at all, which is what the four
        // ticks were already saying and what a parent switch could only
        // contradict.
        return new SchedulingConfiguration(!allowed.AllowsNothing, allowed);
    }
}
