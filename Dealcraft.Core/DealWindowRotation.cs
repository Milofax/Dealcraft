using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Picks which allowed window the next deal goes into.
/// </summary>
/// <remarks>
/// <para>
/// <b>The soonest window from now that the player allows and the game is
/// currently offering.</b> Nothing cleverer, and this class used to be
/// cleverer: it handed windows out in turn so deals would not stack into one of
/// them.
/// </para>
/// <para>
/// The owner threw that out, and his reason settles it — the game does not
/// expose which region a deal is in, so travel cannot be planned, and spreading
/// deals across the day therefore buys nothing while making the choice
/// unpredictable. <i>"Die Region teilt mir ja nicht mit, also können wir das
/// auch nicht optimieren. Abwechslung ist doch totaler Blödsinn."</i>
/// </para>
/// <para>
/// <b>The walk starts after the window the game is in, not at the top of the
/// list.</b> It used to start at <see cref="DealWindowSet.All"/>[0] every time,
/// so with Morning allowed Morning always won, whatever the clock said: an
/// offer taken at 18:50 went into next morning while Night was ticked, on
/// offer, and hours away.
/// </para>
/// <para>
/// <b>The window the clock is in is taken when the game is still offering
/// it.</b> It was skipped, always, for eight days, and the owner paid for it.
/// He had asked for it after a deal taken at 14:25 went into that same afternoon
/// — <i>"Ich dachte immer in die nächste. Und das ist auch ein Bug."</i> — and a
/// reading of <c>DealWindowSelector.IsWindowValid</c> seemed to back it: the game
/// offers a window when its next <em>start</em> is more than two hours away,
/// which was taken to mean that a window the clock stands inside is offered as
/// tomorrow's.
/// </para>
/// <para>
/// <b>The running game said otherwise.</b> On day 43 at 18:19 the owner accepted
/// Mac Cooper's deal into Night by hand, and the contract the game made runs
/// 18:00 to 24:00 and expires that same night. So the current window means
/// tonight; the gate only decides whether the button is enabled. Skipping it had
/// put every evening offer into Late Night, and when he slept through Late Night
/// all ten of them expired: <i>"auf einmal alle Aufträge ... verstrichen"</i>.
/// The rule is now the project's own line — the mod clicks what the player could
/// click, and the game decides what is on offer.
/// </para>
/// <para>
/// So the rule is the project's own line instead of either preference: the mod
/// clicks what the player could click. Whether enough of a window is left is the
/// game's judgement and it is asked for it —
/// <c>DealWindowReader.OnOffer</c> calls <c>DealWindowSelector.IsWindowValid</c>
/// for every window, which is exactly the set of buttons a player would find
/// enabled — so a window running out is withheld by the game rather than by a
/// margin invented here. That also retires the old "the only window you allow is
/// the one the clock is in, so wait" answer: there is nothing left to wait for
/// that the game has not already decided.
/// </para>
/// <para>
/// So this holds no state at all. The same inputs give the same window, every
/// time, and a reload changes nothing.
/// </para>
/// </remarks>
public sealed class DealWindowRotation
{
    /// <summary>
    /// Where midnight falls in <see cref="DealWindowSet.All"/>. The game rolls
    /// its date over from 23:59 to 00:00 —
    /// <c>TimeManager.RpcLogic___PassMinute_Client</c> tests the
    /// clock against 2359 and only there increments <c>ElapsedDays</c> and puts
    /// the time back to zero — and Late Night is the window that starts at
    /// 00:00 (<c>DealWindowInfo..cctor</c>). So a walk forward
    /// through the list crosses into tomorrow exactly when it steps over this
    /// position, and a walk that begins at Late Night never does: Morning,
    /// Afternoon and Night are all still the same date as a 2 a.m. offer.
    /// </summary>
    private const int FirstWindowAfterMidnight = 3;

    /// <summary>
    /// Choose a window for one deal, or say why there is none.
    /// </summary>
    /// <param name="allowed">The windows the host permits at all.</param>
    /// <param name="onOffer">
    /// The windows the game is currently offering — what the player's own
    /// selector would let them click. Never picks outside it.
    /// </param>
    /// <param name="now">
    /// The window the game's clock is in, which is where the walk begins.
    /// Null when the game could not be asked; the walk then starts at the top
    /// of the day and says so rather than claiming a "next" it cannot know.
    /// </param>
    public ScheduleDecision Choose(
        DealWindowSet allowed, IReadOnlyCollection<DealWindow> onOffer, DealWindow? now) =>
        Scan(allowed, onOffer, now);

    /// <summary>
    /// The same answer <see cref="Choose"/> would give, without taking the
    /// turn.
    /// </summary>
    /// <remarks>
    /// Claiming a contract is an act, not a question, so a caller has to be
    /// able to find out whether there is a window at all before it claims one:
    /// a claim spent on a deal that then had nowhere to go would lock that
    /// offer out for the rest of its life. Both answers come from the same scan,
    /// so they cannot disagree.
    /// </remarks>
    public ScheduleDecision Peek(
        DealWindowSet allowed, IReadOnlyCollection<DealWindow> onOffer, DealWindow? now) =>
        Scan(allowed, onOffer, now);

    private static ScheduleDecision Scan(
        DealWindowSet allowed,
        IReadOnlyCollection<DealWindow> onOffer,
        DealWindow? now)
    {
        if (allowed.AllowsNothing)
        {
            return ScheduleDecision.Skip(
                "no deal window is allowed, so nothing is auto-scheduled; "
                + "allow at least one window to turn scheduling back on");
        }

        IReadOnlyList<DealWindow> order = DealWindowSet.All;
        int start = now is null ? -1 : PositionOf(order, now.Value);

        // A start of -1 is "the game could not be asked", and the walk then runs
        // the list from the top — the old behaviour, under a reason that admits
        // it.
        int from = start < 0 ? 0 : start;

        // The walk begins at the window the clock is in, and that window is a
        // candidate like any other. The game is asked whether it is still
        // offering it — DealWindowReader.OnOffer calls
        // DealWindowSelector.IsWindowValid for each one — so taking it is
        // taking a button the player would find enabled, and leaving it is the
        // game's answer rather than a margin invented here.
        int first = 0;
        int last = order.Count - 1;

        for (int step = first; step <= last; step++)
        {
            int at = from + step;
            DealWindow window = order[at < order.Count ? at : at - order.Count];

            if (!allowed.Allows(window) || !Contains(onOffer, window))
            {
                continue;
            }

            return ScheduleDecision.Schedule(window, Because(window, start, step, order.Count));
        }

        return ScheduleDecision.Skip(
            $"the game is offering none of the allowed windows right now (allowed: {allowed})");
    }

    /// <summary>
    /// Why this window, in words that are true of the window that was picked.
    /// The old line said "is the next window you allow" whatever it had found,
    /// which was wrong twice over: it was the first rather than the next, and
    /// with the clock inside it there is a third case — the window the day is
    /// already in, which is not a "next" at all.
    /// </summary>
    private static string Because(DealWindow window, int start, int step, int windows)
    {
        string name = DealWindowName.Of(window);

        if (start < 0)
        {
            return $"{name} is the first window you allow, and the game's clock could not be "
                + "read to count from now";
        }

        if (step == 0)
        {
            return $"{name} is the window the clock is in and the game is still offering it";
        }

        bool tomorrow = start < FirstWindowAfterMidnight
            && start + step >= FirstWindowAfterMidnight;

        return tomorrow
            ? $"{name} is the next window you allow, and it is tomorrow's"
            : $"{name} is the next window you allow today";
    }

    /// <summary>
    /// Where a window sits in the day's order, or -1 for one that is not in it
    /// — which a window cast from a number the game grew later would be.
    /// </summary>
    private static int PositionOf(IReadOnlyList<DealWindow> order, DealWindow window)
    {
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] == window)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Membership without an allocation or an enumerator: this runs inside the
    /// game's update loop, and <paramref name="candidates"/> is four items long
    /// at most.
    /// </summary>
    private static bool Contains(IReadOnlyCollection<DealWindow> candidates, DealWindow window)
    {
        if (candidates is IReadOnlyList<DealWindow> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == window)
                {
                    return true;
                }
            }

            return false;
        }

        foreach (DealWindow candidate in candidates)
        {
            if (candidate == window)
            {
                return true;
            }
        }

        return false;
    }
}
