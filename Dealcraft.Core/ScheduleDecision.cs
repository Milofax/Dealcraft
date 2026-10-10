namespace Dealcraft.Core;

public enum ScheduleOutcome
{
    /// <summary>
    /// Leave this offer alone. See the reason. This is the zero value on
    /// purpose: a decision nobody made must never read as permission to act.
    /// </summary>
    Skip,

    /// <summary>
    /// Nothing stands in the way of scheduling this offer. Claim the contract
    /// and then ask for a window; the claim is what makes it exactly once.
    /// </summary>
    Claim,

    /// <summary>Accept the offer into <see cref="ScheduleDecision.Window"/>.</summary>
    Schedule,
}

/// <summary>
/// What to do about one offer, and why. The reason is always populated and
/// always goes in the log, including when the answer is to do nothing — a
/// scheduler that stands down silently is indistinguishable from a broken one.
/// </summary>
public readonly struct ScheduleDecision
{
    private readonly string? reason;

    private ScheduleDecision(ScheduleOutcome outcome, DealWindow window, string reason)
    {
        Outcome = outcome;
        Window = window;
        this.reason = reason;
    }

    public ScheduleOutcome Outcome { get; }

    /// <summary>
    /// Only meaningful when <see cref="Outcome"/> is
    /// <see cref="ScheduleOutcome.Schedule"/>.
    /// </summary>
    public DealWindow Window { get; }

    public string Reason => reason ?? "nothing was decided";

    public static ScheduleDecision Skip(string reason) =>
        new(ScheduleOutcome.Skip, default, reason);

    public static ScheduleDecision Claim(string reason) =>
        new(ScheduleOutcome.Claim, default, reason);

    public static ScheduleDecision Schedule(DealWindow window, string reason) =>
        new(ScheduleOutcome.Schedule, window, reason);

    public override string ToString() =>
        Outcome == ScheduleOutcome.Schedule
            ? $"{Outcome} into {DealWindowName.Of(Window)} ({Reason})"
            : $"{Outcome} ({Reason})";
}
