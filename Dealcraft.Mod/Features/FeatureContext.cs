using System;
using System.Collections.Generic;
using Dealcraft.Core;
using HarmonyLib;
using UnityEngine;

namespace Dealcraft;

/// <summary>
/// What every feature is given when it starts: the settings, a way to write to
/// the log under a tag of its own, and the other features.
/// </summary>
internal sealed class FeatureContext
{
    private readonly ModSettings _settings;
    private readonly IReadOnlyList<Feature> _features;
    private readonly Action<string> _log;
    private readonly Action<string> _warn;

    public FeatureContext(
        ModSettings settings,
        IReadOnlyList<Feature> features,
        HarmonyLib.Harmony harmony,
        Action<string> log,
        Action<string> warn)
    {
        _settings = settings;
        _features = features;
        Harmony = harmony;
        _log = log;
        _warn = warn;
    }

    /// <summary>
    /// The mod's one Harmony instance, for the rare feature that has to listen
    /// to the game rather than ask it. Only the handover ledger uses it: a patch
    /// on the game's own methods is the only way to see values that are passed
    /// as arguments and never stored, and the ledger's whole purpose is to
    /// record such a value without our arithmetic in between.
    /// </summary>
    /// <remarks>
    /// Null only if the mod is hosted by something that gave MelonLoader none,
    /// which every caller answers by recording nothing rather than by patching
    /// blindly.
    /// </remarks>
    public HarmonyLib.Harmony Harmony { get; }

    /// <summary>
    /// The settings the whole mod shares, read afresh on every call so that a
    /// hand-edited preferences file takes effect without a restart. Features
    /// hold this rather than a reading of it, which is what keeps the file the
    /// only store.
    /// </summary>
    public Func<AdvisorSettings> Settings => _settings.ToAdvisorSettings;


    /// <summary>
    /// Every setting the Automation section shows: the shared ones and then each
    /// feature's own, in registration order. Read through
    /// <see cref="ModSettings"/> so there is still exactly one owner of the
    /// answer.
    /// </summary>
    public IReadOnlyList<AutomationSetting> Automation() => _settings.Describe(_features);

    /// <summary>
    /// Write one setting a player changed in the app, through whoever owns it,
    /// and flush the file. Returns whether anything owned the key.
    /// </summary>
    /// <remarks>
    /// The same <see cref="ModSettings"/> that <see cref="Automation"/> reads
    /// through, so a row is read from the file, changed in the file, and read
    /// back out of the file. No value ever lives only in the app.
    /// </remarks>
    public bool Change(SettingChange change) => _settings.Change(_features, change);

    /// <summary>
    /// Another feature, for the rare case where one genuinely needs a handle on
    /// another — the product insight panel has to tell Dealcraft's own detail
    /// panel from the vanilla one, and only the app feature knows which that is.
    /// Null when it is not registered, which is a feature left out rather than
    /// an error.
    /// </summary>
    public T Other<T>()
        where T : Feature
    {
        foreach (Feature feature in _features)
        {
            if (feature is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// The mod's one reading of whether it may act at all: a save loaded, no
    /// save being written, this machine the host. Every feature that sends one
    /// of the game's calls asks it first, and they all ask the same one so that
    /// a frame costs one reading and no two of them can disagree.
    /// </summary>
    /// <remarks>
    /// Null only if <see cref="LifecycleFeature"/> is left out of the build,
    /// which every caller answers by standing down — a mod without the guard
    /// does nothing rather than acting unguarded.
    /// </remarks>
    public LifecycleWatch Lifecycle() => Other<LifecycleFeature>()?.Watch;

    /// <summary>
    /// A log writer. With a tag, every line is prefixed with it; without one,
    /// the line goes out as written.
    /// </summary>
    public Action<string> Log(string tag = null) => Tagged(_log, tag);

    /// <summary>A warning writer, tagged the same way.</summary>
    public Action<string> Warn(string tag = null) => Tagged(_warn, tag);

    private static Action<string> Tagged(Action<string> write, string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return write;
        }

        return message => write($"[{tag}] {message}");
    }
}
