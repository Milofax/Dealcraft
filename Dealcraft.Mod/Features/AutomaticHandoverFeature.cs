using System;
using Dealcraft.Core;

namespace Dealcraft;

/// <summary>
/// Completes a handover once the game reports it valid. Off on a fresh install
/// like every other automation switch, and — alone among them — not host only:
/// it moves this player's own goods, so it runs for whoever installed Dealcraft.
/// See <see cref="Dealcraft.Core.HandoverGate"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two ways in, and only one of them is a timer.</b> <see cref="Tick"/> is the
/// sweep; the Harmony prefix in <see cref="HandoverDialoguePatches"/> is the
/// player opening a customer's dialogue, which is what <i>when I talk to them</i>
/// acts on. The sweep cannot serve that answer — the owner's session has him
/// beating it by six seconds — and the prefix cannot serve <i>from anywhere</i>,
/// which is about a customer nobody is standing in front of. Both exist because
/// both answers do.
/// </para>
/// <para>
/// <b>Nothing is patched while the handover is on the game's own behaviour.</b>
/// The detour goes on the first tick that reads the switch as on, the way the
/// handover ledger's does, and stays for the session: a player who never
/// automates a handover never has a detour on the game's dialogue. It is not
/// removed again when the switch goes off, because unpatching the game's
/// interaction handling underneath a live session costs far more than the
/// comparison the gate makes anyway.
/// </para>
/// </remarks>
internal sealed class AutomaticHandoverFeature : Feature
{
    private AutomaticHandover _handover;

    private HarmonyLib.Harmony _harmony;

    private Func<AdvisorSettings> _settings;

    private Action<string> _warn;

    private bool _listening;

    /// <summary>Said once: an install that fails must not be retried every frame.</summary>
    private bool _gaveUp;

    public override string Name => "handover";

    public override void Start(FeatureContext context)
    {
        _harmony = context.Harmony;
        _settings = context.Settings;
        _warn = context.Warn(Name);

        // Reads the switch afresh every pass, so turning it off in the file or
        // in the app takes effect without a restart.
        _handover = new AutomaticHandover(
            () => context.Settings().AutoHandover,
            context.Settings,
            context.Lifecycle(),
            context.Log(Name),
            _warn);
    }

    public override void Tick()
    {
        Listen();
        _handover?.Tick();
    }

    /// <summary>
    /// Put the prefix on the game's dialogue the first time the handover is
    /// automated, and never again.
    /// </summary>
    private void Listen()
    {
        if (_listening || _gaveUp || _handover is null || _settings is null
            || !_settings().AutoHandover)
        {
            return;
        }

        if (_harmony is null)
        {
            _gaveUp = true;
            _warn("Automated handover is on, but this mod has no Harmony instance to patch with. "
                + "Handovers will only be completed by the periodic sweep, so 'when I talk to "
                + "them' will act late or not at all.");
            return;
        }

        string failure = HandoverDialoguePatches.Install(_harmony, _handover.WhenTheyAreTalkedTo);
        if (failure is not null)
        {
            _gaveUp = true;
            _warn($"Dealcraft could not listen for customer dialogues ({failure}). Handovers will "
                + "only be completed by the periodic sweep, so 'when I talk to them' will act late "
                + "or not at all.");
            return;
        }

        _listening = true;
    }
}
