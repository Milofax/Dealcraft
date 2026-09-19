using HarmonyLib;
using Il2CppFishNet;
using MelonLoader;

namespace Dealcraft;

/// <summary>
/// Records what every handover actually paid, into
/// <c>UserData/Dealcraft/handover-ledger.jsonl</c>. Off on a fresh install, and
/// deliberately absent from the phone app.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is not in the app.</b> Every other setting this mod has is in the
/// file <em>and</em> in the app, and that rule is checked by a test. This one is
/// the exception, and the exception was asked for: the owner wanted the bonuses
/// tracked so they could be read afterwards, explicitly not made visible again.
/// A row in the Automation section would make it a feature; it is an
/// instrument. So <see cref="Describe"/> returns nothing, <see cref="Change"/>
/// takes nothing, and <c>MelonPreferences.cfg</c> is the only way to turn it on.
/// </para>
/// <para>
/// <b>Off means nothing is installed.</b> No Harmony patch exists until the
/// setting has been read as on, so a host who has not asked for a ledger pays
/// nothing for it — not a detour, not a branch on the handover path. The check
/// is one bool read per frame until then, which is what lets a player turn it on
/// in the file mid-session and have it take effect without a restart, the way
/// every other setting in this mod does.
/// </para>
/// <para>
/// Once installed the patches stay for the session, and the recorder asks the
/// switch again before every row. Turning it off therefore stops the recording
/// immediately; it leaves the detour in place until the game restarts, which
/// costs a comparison and is far safer than unpatching the game's handover path
/// underneath a live deal.
/// </para>
/// </remarks>
internal sealed class HandoverLedgerFeature : Feature
{
    /// <summary>The entry name, which is the whole of this feature's interface.</summary>
    public const string Key = "HandoverLedger";

    private MelonPreferences_Entry<bool> _enabled;

    private HarmonyLib.Harmony _harmony;

    private HandoverLedger _ledger;

    private bool _installed;

    /// <summary>Said once: an install that fails must not be retried every frame.</summary>
    private bool _gaveUp;

    private System.Action<string> _log;

    private System.Action<string> _warn;

    public override string Name => "ledger";

    public override void Declare(MelonPreferences_Category category) =>
        _enabled = category.CreateEntry(
            Key, false,
            description: "Host only: write one line per handover to "
                + "UserData/Dealcraft/handover-ledger.jsonl, recording what the game paid and which "
                + "bonuses it named. Off by default. Nothing is installed while it is off, and it "
                + "never appears in the phone app: this is a measuring instrument, not a feature. "
                + "Read the file with bin/read-ledger.");

    // Describe() and Change() are deliberately not overridden. See the remarks
    // on this class: this is the one setting that lives in the file only.

    public override void Start(FeatureContext context)
    {
        _harmony = context.Harmony;
        _log = context.Log(Name);
        _warn = context.Warn(Name);

        _ledger = new HandoverLedger(
            () => _enabled is not null && _enabled.Value,
            () => InstanceFinder.IsServer,
            new LedgerFile(),
            _log,
            _warn);
    }

    /// <summary>
    /// Install the patches the first time the setting reads as on, and never
    /// again. Everything else about a ledger row happens inside the game's own
    /// call, not here.
    /// </summary>
    public override void Tick()
    {
        if (_installed || _gaveUp || _enabled is null || !_enabled.Value)
        {
            return;
        }

        if (_harmony is null)
        {
            _gaveUp = true;
            _warn("The handover ledger is on, but this mod has no Harmony instance to patch with. "
                + "Nothing is being recorded.");
            return;
        }

        string failure = HandoverLedgerPatches.Install(_harmony, _ledger);
        if (failure is not null)
        {
            _gaveUp = true;
            _warn($"The handover ledger could not be installed ({failure}). Handovers are "
                + "unaffected; nothing is being recorded.");
            return;
        }

        _installed = true;
        _log("Handover ledger on. Recording every handover this host sees, including manual ones.");
    }

    public override string Summary() => _enabled is not null && _enabled.Value
        ? "Handover ledger is ON: every handover is written to "
            + "UserData/Dealcraft/handover-ledger.jsonl."
        : null;
}
