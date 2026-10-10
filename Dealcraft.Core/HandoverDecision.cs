namespace Dealcraft.Core;

public enum HandoverAction
{
    /// <summary>
    /// Automation does not apply here at all — wrong machine, switch off,
    /// excluded customer. The zero value on purpose: a decision nobody made
    /// must never read as permission to act.
    /// </summary>
    Abstain,

    /// <summary>
    /// Everything is in order except the timing. Say nothing and look again
    /// next pass; this is the ordinary state of a scheduled deal.
    /// </summary>
    Wait,

    /// <summary>
    /// The game says this handover is not valid. Do not do it, and log the
    /// reason the game gave.
    /// </summary>
    Refuse,

    /// <summary>Complete the handover.</summary>
    HandOver,
}

public readonly struct HandoverDecision
{
    private readonly string? reason;

    private HandoverDecision(HandoverAction action, string reason)
    {
        Action = action;
        this.reason = reason;
    }

    public HandoverAction Action { get; }

    /// <summary>Why this decision was reached. Always populated; it goes in the log.</summary>
    public string Reason => reason ?? "nothing was decided";

    public static HandoverDecision Abstain(string reason) => new(HandoverAction.Abstain, reason);

    public static HandoverDecision Wait(string reason) => new(HandoverAction.Wait, reason);

    public static HandoverDecision Refuse(string reason) => new(HandoverAction.Refuse, reason);

    public static HandoverDecision HandOver(string reason) => new(HandoverAction.HandOver, reason);

    public override string ToString() => $"{Action} ({Reason})";
}
