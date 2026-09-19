namespace Dealcraft.Core;

/// <summary>
/// One of the game's four deal windows.
/// </summary>
/// <remarks>
/// The numbers are <c>Il2CppScheduleOne.Economy.EDealWindow</c>'s own, read off
/// the 0.4.6f13 interop assemblies, so the adapter casts between the two rather
/// than keeping a translation table that could fall out of step. The order is
/// the order the game day runs in, which is also the order the player's own
/// window selector lays its buttons out in.
/// </remarks>
public enum DealWindow
{
    Morning = 0,
    Afternoon = 1,
    Night = 2,
    LateNight = 3,
}
