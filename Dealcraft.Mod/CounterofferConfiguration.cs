using Dealcraft.Core;

namespace Dealcraft;

/// <summary>
/// Everything a counter-offer sweep reads out of the preferences file, gathered
/// in one place so a sweep works from one consistent reading rather than asking
/// separate entries at separate moments.
/// </summary>
/// <remarks>
/// Read afresh for each sweep, so a hand-edited preferences file shows up in the
/// next sweep rather than at the next restart.
/// </remarks>
internal sealed class CounterofferConfiguration
{
    public CounterofferConfiguration(bool enabled, AdvisorSettings settings)
    {
        Enabled = enabled;
        Settings = settings;
    }

    /// <summary>
    /// The host's <c>AutoCounterOffer</c> switch. Off on a fresh install, and
    /// then nothing negotiates: the player answers their customers by hand,
    /// which is what <c>Manual (game's default)</c> says on the page. It used to
    /// say the Apply button on the advisor overlay was the whole feature; the
    /// overlay was deleted with ticket 27.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// The host's wider configuration, which after the rebuild is the chance
    /// floor and nothing else. The goal, the price bounds, the counter-gain
    /// threshold and the exclusion list were all deleted with the page that
    /// showed them, and this sentence named all four for a while after.
    /// </summary>
    public AdvisorSettings Settings { get; }
}
