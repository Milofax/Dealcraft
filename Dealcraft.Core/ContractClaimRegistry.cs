using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Keeps track of which contracts the automation has already acted on, so that
/// it acts once per contract and never on one a player is handling by hand.
/// </summary>
/// <remarks>
/// <para>
/// Not thread-safe, and deliberately so. The contention this guards against is
/// several machines and several frames converging on one customer's offer, not
/// several threads: every call arrives on the game's main thread, one after
/// another. A lock here would buy nothing and hide that fact.
/// </para>
/// <para>
/// Nothing in here is written to the game's save. After a load the registry
/// starts empty and <see cref="Reconcile"/> rebuilds it from the contracts the
/// game reports as live.
/// </para>
/// </remarks>
public sealed class ContractClaimRegistry
{
    /// <summary>
    /// Room for far more contracts than a session has customers, so the ceiling
    /// is only ever reached by a caller that has stopped reconciling.
    /// </summary>
    public const int DefaultCapacity = 1024;

    private readonly Dictionary<string, ContractState> entries = new(StringComparer.Ordinal);
    private readonly int capacity;

    public ContractClaimRegistry()
        : this(DefaultCapacity)
    {
    }

    /// <param name="capacity">
    /// The most contracts the registry will claim before refusing new ones.
    /// </param>
    public ContractClaimRegistry(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity), capacity, "The registry needs room for at least one contract.");
        }

        this.capacity = capacity;
    }

    /// <summary>How many contracts the registry is currently holding.</summary>
    public int Count => entries.Count;

    /// <summary>
    /// Take ownership of a contract before acting on it. Only
    /// <see cref="ClaimOutcome.Claimed"/> is permission to act; every other
    /// outcome means stand down and log the reason.
    /// </summary>
    /// <param name="contractKey">
    /// A key that identifies this contract for as long as it lives and is the
    /// same on every machine. The caller derives it; the registry only compares
    /// it, exactly, character for character.
    /// </param>
    public ClaimDecision Claim(string contractKey)
    {
        if (string.IsNullOrWhiteSpace(contractKey))
        {
            return ClaimDecision.NotEligible("the contract has no stable key");
        }

        if (entries.TryGetValue(contractKey, out ContractState state))
        {
            switch (state)
            {
                case ContractState.HumanLocked:
                    return ClaimDecision.LockedByHuman($"a player opened '{contractKey}' by hand");
                case ContractState.Claimed:
                    return ClaimDecision.AlreadyClaimed($"'{contractKey}' was already claimed");
                default:
                    return ClaimDecision.AlreadyClaimed($"'{contractKey}' was already handled and released");
            }
        }

        if (entries.Count >= capacity)
        {
            return ClaimDecision.NotEligible(
                $"the claim registry is full at {capacity} contracts and has not been reconciled");
        }

        entries[contractKey] = ContractState.Claimed;
        return ClaimDecision.Claimed($"'{contractKey}' claimed");
    }

    /// <summary>
    /// Report that the automation is done with a contract. The entry stays so
    /// that the contract cannot be claimed a second time while it is still
    /// live; <see cref="Reconcile"/> is what finally forgets it.
    /// </summary>
    public void Release(string contractKey)
    {
        if (string.IsNullOrWhiteSpace(contractKey))
        {
            return;
        }

        if (entries.TryGetValue(contractKey, out ContractState state) && state != ContractState.HumanLocked)
        {
            entries[contractKey] = ContractState.Released;
        }
    }

    /// <summary>
    /// Record that a player has touched this contract themselves. From here on
    /// the automation is locked out of it, whether or not it was ever claimed.
    /// </summary>
    public void NoteHumanInteraction(string contractKey)
    {
        if (string.IsNullOrWhiteSpace(contractKey))
        {
            return;
        }

        entries[contractKey] = ContractState.HumanLocked;
    }

    /// <summary>
    /// Bring the registry back in line with the game: forget every contract not
    /// in <paramref name="liveContractKeys"/>. This is how the registry is
    /// rebuilt after a load and what keeps a long session's memory flat, so
    /// call it periodically — once per scan pass is plenty, never per frame.
    /// </summary>
    /// <param name="liveContractKeys">
    /// Every contract the game currently knows about. A short or stale list
    /// forgets contracts that are still live, so build it from the game and not
    /// from a cache.
    /// </param>
    public void Reconcile(IEnumerable<string> liveContractKeys)
    {
        var live = new HashSet<string>(liveContractKeys, StringComparer.Ordinal);
        var dead = new List<string>();

        foreach (string key in entries.Keys)
        {
            if (!live.Contains(key))
            {
                dead.Add(key);
            }
        }

        foreach (string key in dead)
        {
            entries.Remove(key);
        }
    }

    private enum ContractState
    {
        Claimed,
        Released,
        HumanLocked,
    }
}
