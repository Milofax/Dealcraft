using System;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;

namespace Dealcraft;

/// <summary>
/// Reads two things off the game's persistence managers: whether a save is
/// loaded, and whether one is being written right now.
/// </summary>
/// <remarks>
/// <para>
/// Both come from the game, not from a guess about the game.
/// <c>LoadManager.IsGameLoaded</c> is what says the world exists at all — the
/// spec's "the mod does nothing while no save is loaded" — and
/// <c>SaveManager.IsSaving</c> is the one the original learned to respect the
/// hard way: it gates its auto-responses on it and waits, because acting into
/// the middle of a save is how a deal ends up half-written into the file.
/// </para>
/// <para>
/// Both managers are <c>PersistentSingleton&lt;T&gt;</c>, which inherits
/// <c>InstanceExists</c> and <c>Instance</c> from
/// <c>Singleton&lt;T&gt;</c> — the same access the mod already uses for
/// <c>Singleton&lt;HandoverScreen&gt;</c>. Read off the shipped interop metadata
/// of 0.4.6f13.
/// </para>
/// <para>
/// A reading that fails is answered on the safe side rather than optimistically:
/// no save loaded, and saving in progress. Both answers stop the automation, and
/// stopping is the harmless way to be wrong here.
/// </para>
/// </remarks>
internal sealed class SaveStateReader
{
    private readonly Action<string> _warn;

    /// <summary>The last complaint made, so it is not repeated every frame.</summary>
    private string _complained = string.Empty;

    public SaveStateReader(Action<string> warn)
    {
        _warn = warn;
    }

    /// <summary>
    /// The scene is gone, so what was complained about it is stale. A problem
    /// that survives the new scene is worth saying again.
    /// </summary>
    public void Forget() => _complained = string.Empty;

    /// <summary>Whether the game reports a save as loaded and playable.</summary>
    public bool Loaded()
    {
        try
        {
            if (!Singleton<LoadManager>.InstanceExists)
            {
                return false;
            }

            LoadManager manager = Singleton<LoadManager>.Instance;
            return manager != null && manager.IsGameLoaded;
        }
        catch (Exception error)
        {
            Complain($"the game could not be asked whether a save is loaded: {error.Message}");
            return false;
        }
    }

    /// <summary>
    /// Whether the game is writing a save right now. Unreadable counts as
    /// saving: a pass that waits a frame too long costs nothing, and one that
    /// acts during a save costs a save file.
    /// </summary>
    public bool Saving()
    {
        try
        {
            if (!Singleton<SaveManager>.InstanceExists)
            {
                // No save manager means nothing is being written. This is the
                // main menu, where the gate has already stood the pass down for
                // want of a loaded save.
                return false;
            }

            SaveManager manager = Singleton<SaveManager>.Instance;
            if (manager == null)
            {
                return false;
            }

            return manager.IsSaving;
        }
        catch (Exception error)
        {
            Complain($"the game could not be asked whether it is saving, so the automation waits: "
                + error.Message);
            return true;
        }
    }

    private void Complain(string problem)
    {
        if (problem == _complained)
        {
            return;
        }

        _complained = problem;
        _warn(problem);
    }
}
