using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Which of the things accepting a deal walks through are missing right now.
/// </summary>
/// <remarks>
/// A contract is built in several steps, and the phone and the journal come in
/// the middle. A piece that is missing throws there, after the contract has been
/// half registered and before the offer is cleared, so the pieces are counted
/// before anything is claimed.
/// </remarks>
public static class AcceptPath
{
    /// <summary>
    /// The missing pieces by name, comma separated, or empty when all are up.
    /// </summary>
    public static string Gap(params (string Name, bool Up)[] pieces)
    {
        var missing = new List<string>(pieces.Length);
        foreach ((string name, bool up) in pieces)
        {
            if (!up)
            {
                missing.Add(name);
            }
        }

        return string.Join(", ", missing);
    }
}
