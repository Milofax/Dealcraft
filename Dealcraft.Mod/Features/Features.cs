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
/// is started in this order. Nothing else in the list depends on running before
/// or after anything else, because Dealcraft draws in one place only and that
/// place is its own app.
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
        new DealSchedulingFeature(),
        new CounterofferAutomationFeature(),
        new PriceMaintenanceFeature(),
        new HandoverLedgerFeature(),
    };
}
