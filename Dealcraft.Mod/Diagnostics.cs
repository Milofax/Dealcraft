namespace Dealcraft;

/// <summary>
/// Whether this build keeps a record of what the mod decided.
/// </summary>
/// <remarks>
/// <para>
/// <b>A property of the build, not a setting.</b> The two diagnostic files —
/// <c>decisions.jsonl</c> and <c>handover-ledger.jsonl</c> — are instruments for
/// whoever is working on the mod. They found every real defect this project has
/// had, including five that 767 passing tests did not, so the machine the work
/// happens on keeps them on. A player who installed the mod to play a game is
/// not debugging it and should not be writing a file every time a customer says
/// no.
/// </para>
/// <para>
/// <c>bin/install-dealcraft</c> builds with <c>DEALCRAFT_DEBUG</c> and
/// <c>bin/publish-dealcraft</c> never does, so the released binary cannot have
/// it by accident — there is no switch to leave in the wrong position, and
/// nothing to remember before a release.
/// </para>
/// <para>
/// The handover ledger keeps its own file-only key on top of this. That is
/// deliberate: it is the one instrument a player might be asked to switch on to
/// send a bug report, and a released build can still do that. The decision
/// record has no key at all, in the file or in the app, which is the rule
/// <c>CLAUDE.md</c> states — a preferences key the interface cannot account for
/// is a defect — and this does not weaken it.
/// </para>
/// </remarks>
internal static class Diagnostics
{
#if DEALCRAFT_DEBUG
    /// <summary>
    /// True: this build was made by the install script, on the machine the mod
    /// is worked on.
    /// </summary>
    /// <remarks>
    /// <c>static readonly</c> rather than <c>const</c>: a compile-time constant
    /// folds the other branch away and the build then refuses the file for
    /// unreachable code. The distinction still happens at build time — the
    /// value is decided by <c>#if</c> — it is simply not folded.
    /// </remarks>
    public static readonly bool Recording = true;
#else
    /// <summary>
    /// False: this is a released build. Nothing is written unless a player is
    /// asked to switch the handover ledger on.
    /// </summary>
    /// <remarks>See the other branch: <c>static readonly</c>, not <c>const</c>.</remarks>
    public static readonly bool Recording = false;
#endif

    /// <summary>
    /// What the loaded line says about it, so a bug report carries the answer
    /// without anybody having to ask which build it was.
    /// </summary>
    public static string Describe() =>
        Recording ? "recording every decision" : "not recording; this is a release build";
}
