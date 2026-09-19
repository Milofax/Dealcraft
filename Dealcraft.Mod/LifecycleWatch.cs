using System;
using Dealcraft.Core;
using UnityEngine;

namespace Dealcraft;

/// <summary>
/// The one place the mod asks the game whether it may act at all: is a save
/// loaded, is one being written, and is this machine the server.
/// </summary>
/// <remarks>
/// <para>
/// One watch for the whole mod, not one per feature. The three questions are
/// about the session rather than about any one pass, the answers are the same
/// for all of them on any given frame, and the rule that reads them
/// (<see cref="LifecycleGate"/>) is one rule. Before this existed the save guard
/// covered one pass alone and the three loops that actually send the game's RPCs
/// had none at all.
/// </para>
/// <para>
/// <b>Read once a frame.</b> Every pass asks, some of them more than once, and
/// each question is an interop call into the game. The answers are cached
/// against <c>Time.frameCount</c>, so a frame costs three readings however many
/// times it is asked — and every pass in that frame decides from the same
/// reading, which is what stops two of them disagreeing about whether a save is
/// in progress.
/// </para>
/// <para>
/// Thin on purpose: it reads the game and hands plain values to the core. The
/// rule itself is not here.
/// </para>
/// </remarks>
internal sealed class LifecycleWatch
{
    private readonly SaveStateReader _save;

    /// <summary>
    /// The frame the cached answers belong to. Starts at a value no frame has,
    /// so the first question of the session is always a real reading.
    /// </summary>
    private int _readOnFrame = -1;

    private bool _loaded;
    private bool _saving;
    private ServerAuthority _authority;

    /// <param name="warn">Where a reading that failed is reported, once.</param>
    public LifecycleWatch(Action<string> warn) => _save = new SaveStateReader(warn);

    /// <summary>
    /// Ask a watch that may not be there.
    /// </summary>
    /// <remarks>
    /// Four passes hold one of these and every one of them has to answer the
    /// same question about a build without the lifecycle feature in it. The
    /// answer is to stand down and say why: a wiring mistake that silently
    /// removed the save guard is the one failure this whole file exists to
    /// prevent, so it must be loud and it must stop the pass.
    /// </remarks>
    public static LifecycleVerdict Ask(LifecycleWatch watch, string pass, bool enabled) =>
        watch is null
            ? LifecycleVerdict.StandDown($"{pass} has no lifecycle watch and will not act")
            : watch.Consider(pass, enabled);

    /// <summary>
    /// The scene is gone, so the cached reading and anything complained about it
    /// are stale.
    /// </summary>
    public void SceneChanged()
    {
        _readOnFrame = -1;
        _save.Forget();
    }

    /// <summary>
    /// Whether <paramref name="pass"/> may run this tick, and why not when it
    /// may not. The reason is written for the log and names the pass.
    /// </summary>
    /// <param name="pass">One of <see cref="LifecyclePass"/>.</param>
    /// <param name="enabled">That pass's own switch, off on a fresh install.</param>
    public LifecycleVerdict Consider(string pass, bool enabled)
    {
        Read();

        return LifecycleGate.Consider(new LifecycleConditions
        {
            Pass = pass,
            Enabled = enabled,
            Authority = _authority,
            Shared = LifecyclePass.IsShared(pass),
            SaveLoaded = _loaded,
            Saving = _saving,
        });
    }

    /// <summary>
    /// Ask the game, at most once a frame. Cheap enough to call from anywhere,
    /// which is what lets every caller go through the gate rather than keeping a
    /// reading of its own.
    /// </summary>
    private void Read()
    {
        int frame = Time.frameCount;
        if (frame == _readOnFrame)
        {
            return;
        }

        _readOnFrame = frame;
        _loaded = _save.Loaded();

        // Only worth asking while there is a world to save. On the main menu the
        // gate has already stood every pass down for want of a loaded save.
        _saving = _loaded && _save.Saving();
        _authority = ServerAuthorityReader.Read();
    }
}
