using System;

namespace Dealcraft.Core;

/// <summary>
/// What a pass may do this tick.
/// </summary>
public enum LifecycleOutcome
{
    /// <summary>
    /// A standing condition: the pass is switched off, this machine is not the
    /// server, or no save is loaded. Nothing will change about it this tick or
    /// the next hundred, so a pass may let go of whatever it was holding. The
    /// zero value, so a caller that forgot to ask cannot act by accident.
    /// </summary>
    StandDown,

    /// <summary>
    /// A passing condition — a save being written, a server whose client half is
    /// not up. The work is not going anywhere; keep what is in hand and try
    /// again next tick.
    /// </summary>
    Wait,

    /// <summary>Run the pass.</summary>
    Act,
}

/// <summary>The names the passes are known by, in the log and in this gate.</summary>
/// <remarks>
/// Written down once so that the gate's reasons, the features' log tags and the
/// guard that checks every pass consults the gate all mean the same things.
/// </remarks>
public static class LifecyclePass
{
    public const string Scheduling = "deal scheduling";

    public const string Counteroffers = "counter-offer automation";

    public const string Handover = "automatic handover";

    public const string Prices = "listed price maintenance";

    /// <summary>
    /// Whether a pass acts on state the whole session shares and therefore runs
    /// on the server alone.
    /// </summary>
    /// <remarks>
    /// Written down here rather than at each call site, because the asymmetry is
    /// the multiplayer rule and a rule spelled in four places is a rule that
    /// drifts in three. Negotiating answers one shared conversation, scheduling
    /// writes one shared contract, prices write one shared table; the handover
    /// moves one player's own goods.
    /// </remarks>
    public static bool IsShared(string? pass) =>
        !string.Equals(pass, Handover, StringComparison.Ordinal);
}

/// <summary>The gate's answer, with the reason in the words the log uses.</summary>
public readonly struct LifecycleVerdict
{
    private readonly string? reason;

    private LifecycleVerdict(LifecycleOutcome outcome, string reason, bool worthReporting)
    {
        Outcome = outcome;
        this.reason = reason;
        WorthReporting = worthReporting;
    }

    public LifecycleOutcome Outcome { get; }

    /// <summary>Why. Always populated; it goes in the log.</summary>
    public string Reason => reason ?? "nothing was asked of the lifecycle gate";

    /// <summary>
    /// Whether this is worth a log line at all.
    /// </summary>
    /// <remarks>
    /// Everything is, except a pass being switched off. The mod announces the
    /// state of every switch when it loads, so repeating it once per scene is a
    /// running commentary on a decision the host has already made — and with
    /// four passes and everything off on a fresh install, it is four lines that
    /// say nothing. Every other reason is news: the host asked for the pass and
    /// is entitled to know why it is not running.
    /// </remarks>
    public bool WorthReporting { get; }

    public static LifecycleVerdict StandDown(string reason, bool worthReporting = true) =>
        new(LifecycleOutcome.StandDown, reason, worthReporting);

    public static LifecycleVerdict Wait(string reason) => new(LifecycleOutcome.Wait, reason, true);

    public static LifecycleVerdict Act(string reason) => new(LifecycleOutcome.Act, reason, true);

    public override string ToString() => $"{Outcome} ({Reason})";
}

/// <summary>Everything the gate needs to know, read fresh each tick.</summary>
public struct LifecycleConditions
{
    /// <summary>
    /// Which pass this is, as <see cref="LifecyclePass"/> names it. It appears
    /// in every reason, because four passes share one log and "switched off"
    /// without a subject tells the host nothing.
    /// </summary>
    public string? Pass { get; set; }

    /// <summary>This pass's own switch. Off on a fresh install, all of them.</summary>
    public bool Enabled { get; set; }

    /// <summary>Whether this machine may act on a contract at all.</summary>
    public ServerAuthority Authority { get; set; }

    /// <summary>
    /// Whether this pass acts on state the whole session shares, and therefore
    /// must run on the server alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True for negotiating, scheduling and listed prices: one customer roster,
    /// one contract list, one conversation, one price table. Six installs must
    /// not answer one offer six times.
    /// </para>
    /// <para>
    /// False for the handover, and the asymmetry is the point. A handover is one
    /// player's goods leaving one player's pockets into the customer standing in
    /// front of <em>them</em>, and <c>Customer.IsReadyForHandover</c> reads a
    /// player singleton — on every machine it answers about that machine's own
    /// player. The vanilla call is a client path too:
    /// <c>Customer.ProcessHandover</c> (RVA <c>0x6AF330</c>) reads
    /// <c>IsClientInitialized</c> and calls <c>SendServerRpc</c>, which is the
    /// path a guest's own Done button takes. Nothing in the game forbids it.
    /// </para>
    /// <para>
    /// The zero value is false, and that is deliberate the other way round from
    /// <see cref="ServerAuthority.NotTheServer"/>: a pass that forgets to say it
    /// is shared gets the weaker requirement, so the mistake shows up as an
    /// action happening twice rather than as one silently never happening.
    /// Every pass sets it explicitly, and a test holds the list.
    /// </para>
    /// </remarks>
    public bool Shared { get; set; }

    /// <summary>Whether the game reports a save as loaded and playable.</summary>
    public bool SaveLoaded { get; set; }

    /// <summary>Whether the game is writing a save right now.</summary>
    /// <remarks>
    /// There is no second condition beside it. The guard used to be a setting —
    /// <c>PauseAutomationWhileSaving</c> — and the owner deleted it: <i>"Ich soll
    /// doch nicht entscheiden, was jetzt irgendwie sauber abläuft."</i> Acting
    /// into the middle of a save is how a deal ends up half-written into the
    /// file, so a pass waits and is not asked about it.
    /// </remarks>
    public bool Saving { get; set; }
}

/// <summary>
/// The safety rule every acting pass obeys: whether anything may be done at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The save guard</b> is the lesson the original paid for with save-state
/// bugs. It gates its auto-responses on <c>SaveManager.IsSaving</c> and waits,
/// and so do we: a save is a snapshot of the world, and acting into the middle of
/// one is how a contract ends up half in the file. Waiting costs a frame, which
/// is why <b>the guard is not a setting</b>: it used to be
/// <c>PauseAutomationWhileSaving</c>, and the owner deleted it — <i>"Ich soll
/// doch nicht entscheiden, was jetzt irgendwie sauber abläuft."</i> A pass waits
/// and is not asked about it. See the remarks on
/// <see cref="LifecycleConditions.Saving"/>, which say the same thing; this said
/// the opposite for a while, fifteen lines away from it.
/// </para>
/// <para>
/// <b>No save loaded</b> is the main menu and the seconds of a load before the
/// world exists. The spec's story 19 asks the mod to do nothing there, and those
/// seconds are exactly when a half-built world is easiest to act on.
/// </para>
/// <para>
/// <b>Server authority</b> is the multiplayer rule, and it is not one rule. Six
/// players may each have the mod installed and each see the same replicated
/// state, so a pass acting on <em>shared</em> state runs on the host alone:
/// negotiating, scheduling, listed prices. A pass acting on one player's own
/// possessions runs on that player's machine, because that is whose possessions
/// they are — see <see cref="LifecycleConditions.Shared"/>. The gate has no
/// notion of how many observers there are, which is why a client disconnecting
/// and reconnecting cannot reach it.
/// </para>
/// <para>
/// One gate, not one per pass. It was written for one loop and the three others
/// went without; a rule copied four times is a rule that drifts three times.
/// </para>
/// </remarks>
public static class LifecycleGate
{
    public static LifecycleVerdict Consider(in LifecycleConditions conditions)
    {
        string pass = string.IsNullOrWhiteSpace(conditions.Pass) ? "this pass" : conditions.Pass!;

        // First, so a host who never asked for this feature reads nothing about
        // servers or saving on its account.
        if (!conditions.Enabled)
        {
            return LifecycleVerdict.StandDown($"{pass} is switched off", worthReporting: false);
        }

        if (!conditions.SaveLoaded)
        {
            return LifecycleVerdict.StandDown($"no save is loaded, so {pass} stands down");
        }

        switch (conditions.Authority)
        {
            case ServerAuthority.NotTheServer:
                // No client half either, so even a per-player pass has nothing
                // to send with.
                return LifecycleVerdict.StandDown(
                    conditions.Shared
                        ? $"this machine is not the server, so {pass} is the host's to run"
                        : $"this machine is not connected, so {pass} has nothing to send with");

            case ServerAuthority.Guest:
                // A guest may hand over their own goods and may not answer a
                // conversation the whole session shares.
                if (conditions.Shared)
                {
                    return LifecycleVerdict.StandDown(
                        $"this machine is not the server, so {pass} is the host's to run");
                }

                break;

            case ServerAuthority.WithoutAClient:
                return LifecycleVerdict.Wait(
                    $"the server is not connected as a client yet, and {pass} needs that");
        }

        if (conditions.Saving)
        {
            return LifecycleVerdict.Wait($"the game is saving, so {pass} is standing by");
        }

        return LifecycleVerdict.Act($"{pass} may run");
    }
}
