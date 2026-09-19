using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Every decision and every non-decision, one line each, with the two bounds
/// that keep a session's file finite.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure.</b> It knows how to decide whether a row is worth writing and hands
/// the line to whatever was given to it. Where the file is, and the fact that a
/// full disk must never cost a handover, are <c>LedgerFile</c>'s, which is the
/// one file in the mod allowed to write anything.
/// </para>
/// <para>
/// <b>The first bound is repetition, and it is the one that matters.</b> The
/// passes sweep: the handover scan looks at every customer every two seconds,
/// the negotiation sweep every five, the price pass every minute. Written
/// straight out, a customer standing in the wrong place would cost a row every
/// two seconds for as long as the deal is scheduled, and the file would be a
/// megabyte of the same sentence by morning. So a row is written the first time
/// its subject says it and again only when the words change — the same rule
/// <see cref="SaidOnce"/> already applies to the log, for the same reason, and
/// applied here to the row's <see cref="DebugRow.Digest"/> so that a differing
/// clock does not count as news.
/// </para>
/// <para>
/// <b>The second bound is a ceiling, because the first one is not a proof.</b>
/// Repetition is bounded by how many distinct things the mod can say, and a
/// long enough session with enough customers could still say a great many of
/// them. So the file stops at <see cref="MostRowsInOneSession"/> rows, having
/// written one last row saying that it has — a reader who finds a short file
/// must be able to tell a quiet evening from a full one. Nothing restarts it
/// but restarting the game.
/// </para>
/// <para>
/// <b>Memory stays flat too.</b> What has been said is remembered per subject,
/// and a session that meets more than <see cref="MostSubjectsRemembered"/> of
/// them forgets the lot and says so. Forgetting costs some repeated rows, which
/// the ceiling above already bounds; it does not cost a growing dictionary in a
/// game the player leaves running.
/// </para>
/// <para>
/// Not thread-safe, for the reason <see cref="ContractClaimRegistry"/> is not:
/// every call arrives on the game's main thread.
/// </para>
/// </remarks>
public sealed class DebugRecorder
{
    /// <summary>
    /// The ceiling, and the answer to "how big can this file get". Five
    /// thousand lines is a few hundred kilobytes — more decisions than an
    /// evening produces, and small enough that a reader can open it. A session
    /// writes at most this many decisions plus the one row saying so.
    /// </summary>
    public const int MostRowsInOneSession = 5000;

    /// <summary>
    /// How many subjects the record remembers having spoken about. A roster is
    /// a few dozen customers and a book a few dozen products, so this is room
    /// for a reload or two on top of that rather than a limit anyone meets.
    /// </summary>
    public const int MostSubjectsRemembered = 512;

    private readonly Func<string, bool> _append;

    private readonly Dictionary<string, string> _said = new(StringComparer.Ordinal);

    private int _written;

    /// <param name="append">
    /// Takes one line and answers whether it was written. False closes the
    /// record for the session: a sink that refused one line refuses the next
    /// for the same reason, and asking it again every two seconds would be the
    /// per-frame cost this file is forbidden to have.
    /// </param>
    public DebugRecorder(Func<string, bool> append)
    {
        _append = append ?? throw new ArgumentNullException(nameof(append));
    }

    /// <summary>How many lines have been written this session.</summary>
    public int Written => _written;

    /// <summary>
    /// Whether the record has closed itself — because it filled up, or because
    /// the sink refused a line. Nothing more is written after this is true.
    /// </summary>
    public bool Stopped { get; private set; }

    /// <summary>
    /// Record one decision, if it is not the one already recorded about this
    /// subject.
    /// </summary>
    /// <returns>Whether a line was written.</returns>
    public bool Record(DebugRow row)
    {
        if (row is null || Stopped)
        {
            return false;
        }

        // Checked before the line rather than after it, so the ceiling is the
        // ceiling: the row that says the record filled up is the one line above
        // it, and there is never a session that wrote five thousand and one
        // decisions.
        if (_written >= MostRowsInOneSession)
        {
            Write(DebugRow.AboutTheRecord(
                "Stopped",
                $"this session has written {MostRowsInOneSession} decisions, which is as many as "
                    + "the debug record keeps; nothing further is recorded until the game is "
                    + "restarted, and the automation itself is unaffected"));
            Stopped = true;
            return false;
        }

        // The record's own rows are never held back: they are written at the
        // moment they become true, and a row saying the file stopped must not
        // be dropped for looking like the last one.
        if (row.Decision != DebugDecision.Record && !IsNews(row))
        {
            return false;
        }

        return !Stopped && Write(row);
    }

    /// <summary>
    /// Forget what has been said, so that everything is news again. Called when
    /// the scene changes, which is what a save being reloaded looks like from
    /// here: the contracts are new objects and the reasons are worth hearing
    /// again.
    /// </summary>
    public void Forget() => _said.Clear();

    /// <summary>
    /// Whether this is worth saying about this subject now, and remembering it
    /// if so.
    /// </summary>
    /// <remarks>
    /// The note about forgetting goes back through <see cref="Record"/> rather
    /// than straight to the file, so that it is counted and capped like every
    /// other line. There is only ever one level of that: the note is a
    /// <see cref="DebugDecision.Record"/> row and those do not come back here.
    /// </remarks>
    private bool IsNews(DebugRow row)
    {
        string key = DebugRow.NameOf(row.Decision) + ":" + row.Subject();
        string digest = row.Digest();

        if (_said.TryGetValue(key, out string? last) && string.Equals(last, digest, StringComparison.Ordinal))
        {
            return false;
        }

        if (_said.Count >= MostSubjectsRemembered && !_said.ContainsKey(key))
        {
            _said.Clear();
            Record(DebugRow.AboutTheRecord(
                "Forgot",
                $"more than {MostSubjectsRemembered} subjects have been recorded, so the record "
                    + "has forgotten what it already said; rows below here may repeat rows above"));
        }

        _said[key] = digest;
        return true;
    }

    private bool Write(DebugRow row)
    {
        if (_append(row.ToJson()))
        {
            _written++;
            return true;
        }

        Stopped = true;
        return false;
    }
}
