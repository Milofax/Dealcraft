namespace Dealcraft.Core;

/// <summary>
/// When a scheduled contract actually runs, as the game reports it.
/// </summary>
/// <remarks>
/// <para>
/// These three numbers come from <c>Customer.GetContractTimings</c>, which the
/// adapter calls with the accepted contract's own delivery window. They are not
/// the window's nominal hours and are not derived from them here: the game
/// answers, this carries the answer.
/// </para>
/// <para>
/// The game hands back three zeroes when it has no window to read, and a
/// midnight-to-midnight deal is a real thing, so
/// <see cref="Unavailable"/> is distinguishable from a genuine reading.
/// </para>
/// </remarks>
public readonly struct ContractTimings
{
    /// <summary>
    /// What the game reports when it has nothing to report. Kept apart from a
    /// genuine reading of zero so a failed read is never spelled as a deal at
    /// midnight.
    /// </summary>
    public static readonly ContractTimings Unavailable = default;

    public ContractTimings(int softStartTime, int hardStartTime, int endTime)
    {
        SoftStartTime = softStartTime;
        HardStartTime = hardStartTime;
        EndTime = endTime;
        Available = softStartTime != 0 || hardStartTime != 0 || endTime != 0;
    }

    /// <summary>When the window opens.</summary>
    public int SoftStartTime { get; }

    /// <summary>
    /// The later of the two starts the game reports — in 0.4.6f13, ten minutes
    /// after the soft start.
    /// </summary>
    public int HardStartTime { get; }

    /// <summary>When the window closes.</summary>
    public int EndTime { get; }

    /// <summary>Whether the game supplied a reading at all.</summary>
    public bool Available { get; }
}
