using System;

namespace Dealcraft;

/// <summary>
/// The safety every other feature stands on: whether a save is loaded, whether
/// one is being written, and whether this machine is the host.
/// </summary>
/// <remarks>
/// <para>
/// A feature with no automation of its own. It owns the one save guard the whole
/// mod obeys and the one <see cref="LifecycleWatch"/> the passes ask, and it
/// ticks nothing — the watch answers on demand, so there is nothing to do per
/// frame.
/// </para>
/// <para>
/// It is registered first, and the order matters only for reading: every other
/// feature looks it up in <see cref="Feature.Start"/>, and the watch is built in
/// this feature's constructor rather than in its <c>Start</c>, so a feature that
/// happened to be started first would still find one.
/// </para>
/// </remarks>
internal sealed class LifecycleFeature : Feature
{
    /// <summary>
    /// Where the watch's complaints go. Set when this feature starts, which is
    /// after the watch is built, so the watch is handed an indirection rather
    /// than a writer — a failed reading before the mod is up is dropped instead
    /// of throwing.
    /// </summary>
    private Action<string> _warn;

    /// <summary>
    /// Built in the constructor rather than in <see cref="Start"/> so that no
    /// other feature's start order can find it missing.
    /// </summary>
    private readonly LifecycleWatch _watch;

    public LifecycleFeature()
    {
        // The save guard is always on and is not a control: there used to be an
        // entry for it in the preferences file and a SAFETY block at the top of
        // the app, and the owner deleted both — "Ich soll doch nicht
        // entscheiden, was jetzt irgendwie sauber abläuft." Acting into the
        // middle of a save is how a deal ends up half-written into the file, so
        // a pass waits and is not asked about it.
        _watch = new LifecycleWatch(message => _warn?.Invoke(message));
    }

    public override string Name => "lifecycle";

    /// <summary>
    /// What every acting pass asks before it does anything. Shared, so that all
    /// of them decide from one reading of the game per frame.
    /// </summary>
    public LifecycleWatch Watch => _watch;

    public override void Start(FeatureContext context) => _warn = context.Warn(Name);

    /// <summary>The save managers are scene objects, and the reading of them dies with the scene.</summary>
    public override void SceneChanged() => _watch.SceneChanged();

    /// <summary>
    /// Nothing. The watch reads the game when a pass asks it, so there is no
    /// work to do on a frame where nothing asked.
    /// </summary>
    public override void Tick()
    {
    }
}
