using System;
using System.Collections.Generic;
using Dealcraft.Core;
using MelonLoader;

[assembly: MelonInfo(typeof(Dealcraft.DealcraftMod), "Dealcraft", "0.7.0", "Milofax", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace Dealcraft;

/// <summary>
/// The mod's entry point, and nothing else. What Dealcraft does is in
/// <see cref="Features.All"/>; this starts them, ticks them and tells them when
/// the scene changed. Nothing here knows what any of them is.
/// </summary>
public sealed class DealcraftMod : MelonMod
{
    private readonly ModSettings _settings = new();
    private readonly IReadOnlyList<Feature> _features = Features.All();

    public override void OnInitializeMelon()
    {
        // Settings first: a feature may read the file the moment it starts, and
        // the features declare their own entries as part of this.
        _settings.Load(_features, message => LoggerInstance.Warning(message));

        var context = new FeatureContext(
            _settings,
            _features,
            HarmonyInstance,
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message));

        foreach (Feature feature in _features)
        {
            Guarded(feature, () => feature.Start(context), "start");
        }

        Announce();
    }

    /// <summary>
    /// A scene has come up, so everything held in the old one is gone. Features
    /// that cache nothing from the scene do nothing here.
    /// </summary>
    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        foreach (Feature feature in _features)
        {
            Guarded(feature, feature.SceneChanged, "handle the new scene");
        }
    }

    public override void OnUpdate()
    {
        foreach (Feature feature in _features)
        {
            feature.Tick();
        }
    }

    /// <summary>
    /// What the host reads in the log at startup: the shared settings, then
    /// whatever each feature has to say about its own.
    /// </summary>
    private void Announce()
    {
        AdvisorSettings settings = _settings.ToAdvisorSettings();

        LoggerInstance.Msg(
            $"Dealcraft loaded. " +
            $"Automation: counter-offer {OnOff(settings.AutoCounterOffer)}, " +
            $"handover {OnOff(settings.AutoHandover)}.");

        foreach (Feature feature in _features)
        {
            string summary = feature.Summary();
            if (!string.IsNullOrWhiteSpace(summary))
            {
                LoggerInstance.Msg(summary);
            }
        }
    }

    /// <summary>
    /// One feature failing to start, or to let go of a dead scene, must not take
    /// the rest of the mod with it. <see cref="Feature.Tick"/> is deliberately
    /// not wrapped: it runs every frame, each feature already guards its own
    /// pass, and a try around the whole loop would only hide which one failed
    /// while still costing every frame.
    /// </summary>
    private void Guarded(Feature feature, Action work, string what)
    {
        try
        {
            work();
        }
        catch (Exception error)
        {
            LoggerInstance.Warning($"[{feature.Name}] could not {what}: {error}");
        }
    }

    private static string OnOff(bool value) => value ? "on" : "off";
}
