namespace Dealcraft.Core;

public enum ClaimOutcome
{
    /// <summary>
    /// The registry will not take this contract on. See the reason. This is the
    /// zero value on purpose: a decision nobody made must never read as
    /// permission to act.
    /// </summary>
    NotEligible,

    /// <summary>The caller now owns this contract and may act on it.</summary>
    Claimed,

    /// <summary>Somebody already claimed it. Do nothing; this is not an error.</summary>
    AlreadyClaimed,

    /// <summary>
    /// A player opened this contract by hand. The automation stays out of it
    /// for the rest of the contract's life.
    /// </summary>
    LockedByHuman,
}

public readonly struct ClaimDecision
{
    private readonly string? reason;

    private ClaimDecision(ClaimOutcome outcome, string reason)
    {
        Outcome = outcome;
        this.reason = reason;
    }

    public ClaimOutcome Outcome { get; }

    /// <summary>Why this decision was reached. Always populated; it goes in the log.</summary>
    public string Reason => reason ?? "no claim was asked for";

    public static ClaimDecision Claimed(string reason) => new(ClaimOutcome.Claimed, reason);

    public static ClaimDecision AlreadyClaimed(string reason) => new(ClaimOutcome.AlreadyClaimed, reason);

    public static ClaimDecision LockedByHuman(string reason) => new(ClaimOutcome.LockedByHuman, reason);

    public static ClaimDecision NotEligible(string reason) => new(ClaimOutcome.NotEligible, reason);

    public override string ToString() => $"{Outcome} ({Reason})";
}
