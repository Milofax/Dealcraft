using System;
using Dealcraft.Core;

namespace Dealcraft;

/// <summary>
/// The one line each of the mod's four passes adds to say what it decided.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is static when nothing else here is.</b> Four passes that know
/// nothing about each other all write to one record, and none of them is
/// improved by carrying it: a recorder threaded through four constructors would
/// put an instrument in four signatures that are about negotiating, scheduling,
/// handing over and pricing. <see cref="DebugRecorder"/> holds everything that
/// can be got wrong — the bound, the repetition, the ceiling — and is a plain
/// object driven by tests without the game. What is left here is the wiring, and
/// wiring is all this may ever hold.
/// </para>
/// <para>
/// <b>Whether anything happened is given, never inferred from the verdict.</b>
/// A gate saying <c>Send</c>, <c>Schedule</c>, <c>HandOver</c> or <c>Write</c>
/// is where an attempt starts and not where it ends: the game's own call can
/// still refuse, a screen can be missing, the bag can turn out not to cover the
/// order. A row that read <c>acted</c> off the outcome would claim every one of
/// those went through, which is the one lie this file must not be able to tell.
/// </para>
/// <para>
/// <b>It cannot change what the mod does.</b> Every method answers nothing, and
/// every one of them is wrapped: a record that threw would throw out of the
/// game's update loop, through a pass that was in the middle of a deal. The
/// recorder does not throw and the file below it does not throw, so the wrapper
/// should never fire — which is the reason to have it, since the one thing worse
/// than losing a row is losing a handover to keep one.
/// </para>
/// <para>
/// <b>It is on, and there is no setting.</b> <c>app.md</c> deletes every control
/// the page does not draw and this is not one of them; a preferences key with no
/// row is what <c>CLAUDE.md</c> calls a defect. Off is therefore only ever a
/// mod running without <see cref="DebugRecordFeature"/> having started, and a
/// pass that finds no record open simply says nothing.
/// </para>
/// </remarks>
internal static class DecisionRecord
{
    private static DebugRecorder _recorder;

    /// <summary>Start recording into this. Called once, by the feature.</summary>
    public static void Open(DebugRecorder recorder) => _recorder = recorder;

    /// <summary>
    /// A reloaded save is news again: the contracts are new objects and the
    /// reasons are worth hearing against the new session, which is the same
    /// thing every pass does to its own log memory when the scene changes.
    /// </summary>
    public static void Forget() => _recorder?.Forget();

    /// <summary>What one standing offer came to, including when it came to nothing.</summary>
    /// <param name="probe">
    /// What the probing came to, where a search ran: the item list handed to the
    /// game, the first and last answers, and anything that threw. This is the
    /// half the chance alone cannot say — see <see cref="ProbeDiagnosis"/>.
    /// </param>
    public static void Negotiation(
        CounterofferOutcome outcome,
        bool acted,
        string reason,
        string customerName,
        string contractKey,
        string productId = null,
        int? quantity = null,
        float? price = null,
        float? chance = null,
        float? offeredPayment = null,
        string probe = null) =>
        Write(DebugRow.Negotiation(
            outcome.ToString(),
            acted,
            reason,
            customerName,
            contractKey,
            productId,
            quantity,
            price,
            chance,
            offeredPayment,
            probe));

    /// <summary>Which window one offer went into, or why none.</summary>
    public static void Scheduling(
        ScheduleOutcome outcome,
        bool acted,
        string reason,
        string customerName,
        string contractKey,
        DealWindow? window = null) =>
        Write(DebugRow.Scheduling(
            outcome.ToString(),
            acted,
            reason,
            customerName,
            contractKey,
            window.HasValue ? DealWindowName.Of(window.Value) : null));

    /// <summary>What one contract's handover came to.</summary>
    public static void Handover(
        HandoverAction outcome,
        bool acted,
        string reason,
        string customerName,
        string contractKey,
        string productId = null,
        int? grade = null,
        int? packages = null,
        int? units = null,
        float? contractPayment = null) =>
        Write(DebugRow.Handover(
            outcome.ToString(),
            acted,
            reason,
            customerName,
            contractKey,
            productId,
            grade,
            packages,
            units,
            contractPayment));

    /// <summary>
    /// What one product's listed price came to. With the <c>Products</c> tab
    /// deleted, a price Dealcraft wrote is visible nowhere in the game — this is
    /// the only record that it moved.
    /// </summary>
    public static void ListedPrice(
        ListedPriceOutcome outcome,
        bool acted,
        string reason,
        string productId,
        float? oldPrice = null,
        float? newPrice = null) =>
        Write(DebugRow.ListedPrice(
            outcome.ToString(),
            acted,
            reason,
            productId,
            oldPrice,
            newPrice));

    private static void Write(DebugRow row)
    {
        try
        {
            _recorder?.Record(row);
        }
        catch (Exception)
        {
            // Nothing below here is expected to throw, and a record that did
            // must not take the deal it was recording with it. There is nowhere
            // to report it either: the report would go through the same passes.
        }
    }
}
