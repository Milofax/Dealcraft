using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Keeps a scanning loop from saying the same thing about the same contract
/// every pass.
///
/// The handover scan looks at every customer several times a minute for as long
/// as a deal is scheduled, and most of what it finds is unchanged. "A dealer is
/// handling this contract" is worth one line, not one line every two seconds
/// until the window closes — but it <em>is</em> worth another line if the reason
/// becomes a different one.
///
/// Pure, and not thread-safe for the same reason
/// <see cref="ContractClaimRegistry"/> is not: every call arrives on the game's
/// main thread.
/// </summary>
public sealed class SaidOnce
{
    private readonly Dictionary<string, string> spoken = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether this is worth saying about this contract now. True the first
    /// time, and again whenever the words differ from the last ones.
    /// </summary>
    public bool ShouldSay(string contractKey, string message)
    {
        if (string.IsNullOrWhiteSpace(contractKey))
        {
            return false;
        }

        if (spoken.TryGetValue(contractKey, out string? last) && string.Equals(last, message, StringComparison.Ordinal))
        {
            return false;
        }

        spoken[contractKey] = message;
        return true;
    }

    /// <summary>
    /// Forget every contract that is no longer live, so a long session's memory
    /// stays flat and a contract that comes back is news again. Call it with
    /// the same list the claim registry is reconciled against.
    /// </summary>
    public void Forget(IEnumerable<string> liveContractKeys)
    {
        var live = new HashSet<string>(liveContractKeys, StringComparer.Ordinal);
        var dead = new List<string>();

        foreach (string key in spoken.Keys)
        {
            if (!live.Contains(key))
            {
                dead.Add(key);
            }
        }

        foreach (string key in dead)
        {
            spoken.Remove(key);
        }
    }
}
