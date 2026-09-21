using Dealcraft.Core;

namespace Dealcraft;

/// <summary>
/// Opens the debug record, into
/// <c>UserData/Dealcraft/decisions.jsonl</c>, on a build that records. No
/// setting anywhere: whether it records is a property of the build, and
/// <see cref="Diagnostics"/> says which build this is.
/// </summary>
/// <remarks>
/// <para>
/// <b>It replaces the two tabs that were deleted.</b> The owner will not read
/// numbers on a screen — <c>app.md</c> deletes every status word, every counter
/// and both insight tabs — but a file he never opens can be read by whoever is
/// debugging, and it is what stage 2 of <c>proof.md</c> reads.
/// </para>
/// <para>
/// <b>No setting, deliberately, and this is not the ledger's exemption.</b> The
/// handover ledger is a file-only setting, which is an exception
/// <c>AutomationForm.FileOnly</c> names and the app's guards hold to its terms.
/// This is not one: it has no key at all, in the file or in the app, so there is
/// nothing for the two to disagree about. <c>app.md</c> draws four blocks and
/// nine controls, this is not among them, and a preferences key the interface
/// cannot account for is what <c>CLAUDE.md</c> calls a defect.
/// </para>
/// <para>
/// <b>What being on costs while nothing is happening: nothing.</b>
/// <see cref="Tick"/> compares two numbers. A pass that reaches the same verdict
/// about the same contract on the next sweep writes no line at all, and a
/// session where no automation is switched on never creates the file — the
/// folder and the file are made by the first row, not by starting.
/// </para>
/// </remarks>
internal sealed class DebugRecordFeature : Feature
{
    private LedgerFile _file;

    private DebugRecorder _recorder;

    private System.Action<string> _log;

    private System.Action<string> _warn;

    /// <summary>Said once, when the first row has proved where the file is.</summary>
    private bool _saidWhereItIs;

    /// <summary>Said once, when the record closes itself.</summary>
    private bool _saidItStopped;

    public override string Name => "record";

    // Declare(), Describe() and Change() are deliberately not overridden. This
    // feature has no setting: see the remarks on the class.

    public override void Start(FeatureContext context)
    {
        _log = context.Log(Name);
        _warn = context.Warn(Name);

        // A released build records nothing. Nothing is opened, no folder is
        // made, and DecisionRecord keeps the recorder it starts with, which
        // throws every row away. See Diagnostics.
        if (!Diagnostics.Recording)
        {
            return;
        }

        _file = new LedgerFile(LedgerFile.DecisionsFileName);
        _recorder = new DebugRecorder(_file.Append);

        DecisionRecord.Open(_recorder);
    }

    /// <summary>
    /// A new scene is a save reloaded, so everything the record has already said
    /// is about contracts that no longer exist.
    /// </summary>
    public override void SceneChanged() => DecisionRecord.Forget();

    /// <summary>
    /// Two comparisons, so that the two things worth saying out loud are said
    /// once each: where the file turned out to be, and that it has stopped.
    /// Nothing is written here — the rows come from the passes.
    /// </summary>
    public override void Tick()
    {
        if (_recorder is null)
        {
            return;
        }

        if (!_saidWhereItIs && _recorder.Written > 0)
        {
            _saidWhereItIs = true;
            _log($"Recording every decision to {_file.Path}.");
        }

        if (!_saidItStopped && _recorder.Stopped)
        {
            _saidItStopped = true;
            _warn(_file.Failed
                ? $"Dealcraft has stopped recording decisions ({_file.Failure}). The automation "
                    + "itself is unaffected; restart the game to record again."
                : "Dealcraft has recorded as many decisions as it keeps for one session and has "
                    + "stopped. The automation itself is unaffected; restart the game for a fresh "
                    + "record.");
        }
    }

    public override string Summary() =>
        "Every decision is recorded to UserData/Dealcraft/" + LedgerFile.DecisionsFileName
            + ". Read it with bin/read-ledger.";
}
