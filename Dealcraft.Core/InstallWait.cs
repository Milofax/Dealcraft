namespace Dealcraft.Core;

/// <summary>
/// What one look at the scene found.
/// </summary>
public enum SceneLook
{
    /// <summary>
    /// Nothing of the sort in the scene. It may still be coming — a scene comes
    /// up before its UI does — or this may be a scene that never has one. The
    /// zero value, so a caller that forgot to answer waits rather than acts.
    /// </summary>
    Absent,

    /// <summary>
    /// There, but not usable yet: a pointer the game fills in for itself and has
    /// not filled in, a copy that cannot yet be told from the original. A state
    /// of the scene, not a fault, and the only way it ever changes is by looking
    /// again.
    /// </summary>
    NotReady,

    /// <summary>
    /// The look settled it: installed, or refused for a reason another look will
    /// not change. Either way there is nothing left to wait for.
    /// </summary>
    Settled,
}

/// <summary>
/// What to do after a look.
/// </summary>
public enum InstallVerdict
{
    /// <summary>Look again on a later frame.</summary>
    LookAgain,

    /// <summary>Stop. The look settled it, and it has already been reported.</summary>
    Done,

    /// <summary>
    /// Time is up and nothing of the sort ever appeared. Ordinary for a scene
    /// that has no phone in it, and worth one quiet line.
    /// </summary>
    NeverAppeared,

    /// <summary>
    /// Time is up; it appeared, and never became usable. Not ordinary: something
    /// this scene has was waited for and never arrived, and the difference from
    /// <see cref="NeverAppeared"/> is the whole reason a caller is told which.
    /// </summary>
    NeverReady,
}

/// <summary>
/// How long to keep looking for a piece of the game before deciding it is not
/// coming, and what to say when that runs out.
/// </summary>
/// <remarks>
/// <para>
/// An install that samples the scene once, at scene-init, and reads failure out
/// of an empty value is wrong twice over. The scene is still coming up: the
/// objects are there before the pointers between them are, so "empty" at that
/// moment means "not yet", not "never". And the conclusion is permanent, so one
/// early look decides the whole session.
/// </para>
/// <para>
/// This is the rule the phone installs share, kept apart from the game so it can
/// be read and tested on its own: keep looking; distinguish nothing-there from
/// there-but-not-ready; give up on a bound so a scene that genuinely has no
/// phone does not poll forever; and say which of the two it was, once.
/// </para>
/// <para>
/// The bound is given again the first time the scene shows something, because a
/// wait that has got somewhere has earned its patience — a phone that appears at
/// the end of a slow load should still get a full wait for the pointers inside
/// it. That caps the whole wait at twice the patience.
/// </para>
/// <para>
/// Pure: the clock is passed in. Not thread-safe, and does not need to be —
/// every look arrives on the game's main thread.
/// </para>
/// </remarks>
public sealed class InstallWait
{
    /// <summary>
    /// Long enough for a save to finish loading on a slow machine, short enough
    /// that the main menu is not polled all evening.
    /// </summary>
    public const float DefaultPatienceSeconds = 30f;

    private readonly float patience;

    private bool started;
    private float deadline;
    private bool seen;
    private bool finished;

    /// <param name="patienceSeconds">
    /// How long to wait. A figure that is not a positive number is the caller
    /// having no opinion, and the default is used: a wait is bounded by a real
    /// number of seconds whatever it is asked for.
    /// </param>
    public InstallWait(float patienceSeconds)
    {
        patience = patienceSeconds > 0f ? patienceSeconds : DefaultPatienceSeconds;
    }

    /// <summary>
    /// What to do, given what this look found and when it was taken.
    /// </summary>
    /// <param name="look">What the caller found this time.</param>
    /// <param name="now">
    /// The clock, in seconds, from whatever origin the caller likes. Only
    /// differences are used, and it is expected to go forwards.
    /// </param>
    /// <returns>
    /// <see cref="InstallVerdict.LookAgain"/> until something ends the wait.
    /// Once it has ended, every later look answers <see cref="InstallVerdict.Done"/>,
    /// so a caller that keeps looking cannot make the wait report itself twice.
    /// </returns>
    public InstallVerdict Look(SceneLook look, float now)
    {
        if (finished)
        {
            return InstallVerdict.Done;
        }

        if (!started)
        {
            started = true;
            deadline = now + patience;
        }

        if (look == SceneLook.Settled)
        {
            finished = true;
            return InstallVerdict.Done;
        }

        if (look == SceneLook.NotReady && !seen)
        {
            // The first sign of the thing being there at all. The clock starts
            // again, once, so a phone that turns up late still gets a full wait
            // for the pointers inside it.
            seen = true;
            deadline = now + patience;
        }

        if (now <= deadline)
        {
            return InstallVerdict.LookAgain;
        }

        finished = true;
        return seen ? InstallVerdict.NeverReady : InstallVerdict.NeverAppeared;
    }
}
