using Dealcraft.Core;

namespace Dealcraft;

/// <summary>
/// Everything the scheduler reads out of the preferences file, gathered in one
/// place so a sweep works from one consistent reading rather than asking four
/// separate entries at four separate moments.
/// </summary>
/// <remarks>
/// Read afresh for each sweep, so a hand-edited preferences file shows up in
/// the next sweep rather than at the next restart.
/// </remarks>
internal sealed class SchedulingConfiguration
{
    public SchedulingConfiguration(bool enabled, DealWindowSet windows)
    {
        Enabled = enabled;
        Windows = windows;
    }

    /// <summary>
    /// Whether scheduling may act at all, which is whether any window is
    /// allowed. There is no switch above the four windows.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>Which of the four windows the host allows.</summary>
    public DealWindowSet Windows { get; }
}
