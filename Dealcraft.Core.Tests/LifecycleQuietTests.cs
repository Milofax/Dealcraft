using System.Collections.Generic;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The ticket's last criterion, stated where it can be checked: "a failure that
/// repeats every tick must be reported once and then stay quiet until the
/// condition changes."
///
/// Every gate verdict is a candidate log line and the gate is asked on every
/// frame, so the interesting number is not what one verdict says but how many
/// lines a <em>run</em> of them produces. These tests drive the gate the way the
/// game drives it — a few hundred frames of one condition — and count.
/// </summary>
public class LifecycleQuietTests
{
    /// <summary>
    /// A save takes a few seconds, which at sixty frames a second is a few
    /// hundred passes through the gate. The host reads one line about it, and
    /// then nothing at all: a pass getting on with its work is not news, and the
    /// alternative is a line every time the game autosaves for the rest of the
    /// session.
    /// </summary>
    [Fact]
    public void A_whole_save_costs_one_line_and_finishing_costs_none()
    {
        var log = new List<string>();
        var said = new SaidOnce();

        Report(said, log, Frames(300, Everything() with { Saving = true }));
        Report(said, log, Frames(300, Everything()));

        Assert.Single(log);
        Assert.Contains("saving", log[0]);
    }

    /// <summary>
    /// The case that made this worth writing down: a host who never turned a
    /// feature on. The gate stands the pass down on every single frame of the
    /// session, and the log does not grow at all — the mod announced the switch
    /// when it loaded, and a host who set it themselves does not need telling
    /// ten thousand times, or once.
    /// </summary>
    [Fact]
    public void A_pass_that_is_switched_off_all_session_costs_nothing_at_all()
    {
        var log = new List<string>();
        var said = new SaidOnce();

        Report(said, log, Frames(10_000, Everything() with { Enabled = false }));

        Assert.Empty(log);
    }

    /// <summary>
    /// Quiet is not silent. When the reason changes the host hears about it,
    /// because the new reason is the news — a pass that was waiting for a save
    /// and is now waiting for the server's client half is in a different place,
    /// and coming back to the first is a third thing worth saying.
    /// </summary>
    [Fact]
    public void A_condition_that_changes_is_said_again()
    {
        var log = new List<string>();
        var said = new SaidOnce();

        Report(said, log, Frames(50, Everything() with { Saving = true }));
        Report(said, log, Frames(50, Everything() with { Authority = ServerAuthority.WithoutAClient }));
        Report(said, log, Frames(50, Everything() with { Saving = true }));

        Assert.Equal(3, log.Count);
    }

    /// <summary>
    /// The passes share one log and one gate. Each is a subject of its own, so
    /// the scheduling sweep going quiet does not swallow the handover scan's
    /// first word — and, because the gate names the pass in every reason, the
    /// host can tell which is which.
    /// </summary>
    [Fact]
    public void Each_pass_gets_its_own_line_and_only_one()
    {
        var log = new List<string>();
        var said = new SaidOnce();

        for (var frame = 0; frame < 500; frame++)
        {
            foreach (string pass in new[]
                     {
                         LifecyclePass.Scheduling,
                         LifecyclePass.Counteroffers,
                         LifecyclePass.Handover,
                     })
            {
                Report(said, log, Frames(1, Everything() with { Pass = pass, Saving = true }));
            }
        }

        Assert.Equal(3, log.Count);
        Assert.Contains(log, line => line.Contains(LifecyclePass.Scheduling));
        Assert.Contains(log, line => line.Contains(LifecyclePass.Handover));
    }

    /// <summary>
    /// The long session. The game autosaves over and over across an evening, and
    /// every one of them holds the automation for a second or two. That is the
    /// guard working, not a fault, so the host is told the first time and never
    /// again — thirty saves is one line, not thirty.
    /// </summary>
    [Fact]
    public void An_evenings_worth_of_autosaves_costs_one_line()
    {
        var log = new List<string>();
        var said = new SaidOnce();

        for (var save = 0; save < 30; save++)
        {
            Report(said, log, Frames(120, Everything() with { Saving = true }));
            Report(said, log, Frames(3_000, Everything()));
        }

        Assert.Single(log);
        Assert.Contains("saving", log[0]);
    }

    private static IEnumerable<LifecycleConditions> Frames(int count, LifecycleConditions conditions)
    {
        for (var frame = 0; frame < count; frame++)
        {
            yield return conditions;
        }
    }

    /// <summary>
    /// The adapter's loop, in miniature, and deliberately the same three
    /// conditions each of the four passes applies: say nothing when the pass may
    /// run, say nothing about a switch the host set themselves, and otherwise
    /// say the reason unless it is already the reason standing.
    /// </summary>
    private static void Report(
        SaidOnce said, List<string> log, IEnumerable<LifecycleConditions> frames)
    {
        foreach (LifecycleConditions conditions in frames)
        {
            LifecycleVerdict verdict = LifecycleGate.Consider(conditions);

            if (verdict.Outcome == LifecycleOutcome.Act)
            {
                continue;
            }

            if (verdict.WorthReporting && said.ShouldSay(conditions.Pass!, verdict.Reason))
            {
                log.Add(verdict.Reason);
            }
        }
    }

    private static LifecycleConditions Everything() => new()
    {
        Pass = LifecyclePass.Scheduling,
        Enabled = true,
        Shared = true,
        Authority = ServerAuthority.Held,
        SaveLoaded = true,
        Saving = false,
    };
}
