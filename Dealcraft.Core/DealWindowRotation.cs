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
/// <b>The current window is skipped, and that is the owner's call.</b> It used
/// to be taken when the game still offered it, on the reasoning that
/// <c>DealWindowSelector.IsWindowValid</c> (RVA 0x9FA3D0) already withholds a
/// window with too little of it left — it does read window info against the
/// clock, though the exact margin is not something this project has read out of
/// the machine code. He met it on 2026-09-19, when a deal taken at 14:25 went
/// into that same afternoon, and said plainly that he expects the next one:
/// <i>"Ich dachte immer in die nächste. Und das ist auch ein Bug."</i> He was
/// shown what it costs — an offer at 12:05 now waits for tomorrow morning
/// rather than using five and a half free hours — and chose it anyway.
/// </para>
/// <para>
/// <b>And when the only allowed window is the one the clock is in, nothing is
/// scheduled at all until it ends.</b> The accept hands the game an
/// <c>EDealWindow</c> and nothing else — <c>PlayerAcceptedContract</c> takes no
/// date — so which occurrence of Morning it means is the game's to decide, and
/// while its clock is inside Morning that is today's, the one running out. A
/// decision that named tomorrow would be a sentence in the log rather than a
/// fact about the deal. So the walk comes back round to that window in order to
/// recognise the case, and answers with a wait:
/// <i>"dann musst du warten bis es Mittag ist und dann für den nächsten Morgen
/// einplanen."</i> The cost is an offer sitting unscheduled while the window
/// runs down, and an offer that expires first is one this never gets.
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
    /// <c>TimeManager.RpcLogic___PassMinute_Client</c> (RVA 0xA92EA0) tests the
    /// clock against 2359 and only there increments <c>ElapsedDays</c> and puts
    /// the time back to zero — and Late Night is the window that starts at
    /// 00:00 (<c>DealWindowInfo..cctor</c>, RVA 0x6C1260). So a walk forward
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

        // The window the clock is already in is skipped, and the walk runs one
        // step further than there are windows so that it comes back round to
        // that same window — not to schedule into it, but to recognise that it
        // is the only one left and say so. With the clock unreadable there is
        // no "current" to skip, so that walk starts where it always did.
        int first = start < 0 ? 0 : 1;
        int last = start < 0 ? order.Count - 1 : order.Count;

        for (int step = first; step <= last; step++)
        {
            int at = from + step;
            DealWindow window = order[at < order.Count ? at : at - order.Count];

            if (!allowed.Allows(window) || !Contains(onOffer, window))
            {
                continue;
            }

            // A full lap means the only window left is the one the clock is
            // standing in, and there is no way to ask for the next occurrence
            // of it: the accept hands the game an EDealWindow and nothing else
            // (DealSchedulingLoop calls PlayerAcceptedContract((EDealWindow)
            // window)), so the game decides which occurrence that is, and while
            // its clock is inside the window that is the one running out now.
            // Waiting is the only way to reach the next one, and the owner
            // named it: "dann musst du warten bis es Mittag ist und dann für
            // den nächsten Morgen einplanen."
            if (step >= order.Count)
            {
                return ScheduleDecision.Skip(
                    $"{DealWindowName.Of(window)} is the only window you allow and the clock is in "
                    + "it, so accepting now would put the deal into the one that is running out; "
                    + "waiting for it to end and taking the next one");
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

        // A full lap is the window the day is already in, come round again, so
        // it is tomorrow's whatever the midnight arithmetic below would say —
        // and that arithmetic cannot tell, because it only knows where the walk
        // started and how far it went.
        bool tomorrow = step >= windows
            || (start < FirstWindowAfterMidnight && start + step >= FirstWindowAfterMidnight);

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
