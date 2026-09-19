using System.Collections.Generic;

namespace Dealcraft;

/// <summary>
/// Everything Dealcraft does, in the order it is ticked.
/// </summary>
/// <remarks>
/// <para>
/// This list is the registration seam. A new feature is a new file plus a line
/// here; it does not touch the mod entry point, and it does not touch the
/// settings class. Two features added at the same time collide on one line of a
/// list, which git resolves by keeping both, rather than on a constructor, an
/// update method and a field list, which it cannot.
/// </para>
/// <para>
/// Order is the order of this array and nothing else. It matters in two places.
/// The lifecycle feature comes first because it owns the save guard every other
/// feature asks about, and reading first in the list is the clearest way to say
/// so — though it builds its watch in its constructor, so nothing actually
/// breaks if it moves. The debug record comes next because every pass below it
/// writes to it, and a pass that decided something before the record was opened
/// would decide it unrecorded; <c>Start</c> is what opens it, and every feature
/// is started in this order.
/// </para>
/// <para>
/// <b>And it matters in a third place, which this paragraph used to deny.</b>
/// It said nothing else in the list depended on running before or after
/// anything else. The owner's session of 2026-09-19 disproved it: with both
/// switches on, his deals went through at exactly the price the customer had
/// asked for, and <c>decisions.jsonl</c> held not one negotiation — not even a
/// refused one. Scheduling ran first, accepted the standing offer into an
/// allowed window, and by the time the counteroffer pass looked there was no
/// offer left on the table. <see cref="CounterofferLoop"/> needs
/// <c>HasOfferedContract</c>, and a customer with nothing on the table is not
/// news, so it did not even say so.
/// </para>
/// <para>
/// So countering comes before accepting, which is also the order a person would
/// do it in: ask for a better price first, take the deal second. The two keep
/// their separate claim registries — countering an offer and accepting it into
/// a window are two acts on one contract, and one registry would let the first
/// stand in for the second — so this ordering is the whole of what makes them
/// take turns.
/// </para>
/// </remarks>
internal static class Features
{
    public static IReadOnlyList<Feature> All() => new Feature[]
    {
        new LifecycleFeature(),
        new DebugRecordFeature(),
        new PhoneAppFeature(),
        new AutomaticHandoverFeature(),
        new CounterofferAutomationFeature(),
        new DealSchedulingFeature(),
        new PriceMaintenanceFeature(),
        new HandoverLedgerFeature(),
    };
}
