using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// How a standing offer is named for <see cref="ContractClaimRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// The NPC's id and the moment the offer arrived. Both are the game's, both are
/// replicated to every client through the vanilla <c>SetOfferedContract</c>
/// observer RPC, and together they are the same string on every machine in the
/// session — which is what makes "once per contract" mean once across the whole
/// session rather than once per machine.
/// </para>
/// <para>
/// One derivation, here, for every feature that claims an offered contract. Two
/// of them spelling the key differently would be two contracts where the game
/// has one, and the whole of "exactly once" rests on them agreeing.
/// </para>
/// <para>
/// <b>The day index.</b> The arrival is the game's own day counter and its HHMM
/// clock. The counter is elapsed days and never wraps, so the same clock time on
/// two days is two keys and a week rolling over brings nothing back round. The
/// key takes no notion of "now", so an offer that stands from late evening into
/// the small hours cannot change identity underneath a claim that is already
/// held.
/// </para>
/// </remarks>
public static class ContractKey
{
    /// <param name="npcId">
    /// The customer's NPC id. Blank when the adapter could not read one — a
    /// customer that despawned mid-read — and then there is no key, because a
    /// contract nobody can name must not be acted on.
    /// </param>
    /// <param name="elapsedDays">
    /// The game's elapsed-day counter at the moment the offer arrived, from
    /// <c>Customer.OfferedContractTime</c>.
    /// </param>
    /// <param name="timeOfDay">
    /// The game's HHMM clock at that moment — 2330 for half past eleven at
    /// night. Carried as the game spells it and never used for arithmetic.
    /// </param>
    public static string ForOffer(string? npcId, int elapsedDays, int timeOfDay)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            return string.Empty;
        }

        // Invariant throughout: the key is compared across machines, and a
        // culture that groups digits would make two of them out of one.
        return string.Format(
            CultureInfo.InvariantCulture, "{0}#{1}:{2}", npcId, elapsedDays, timeOfDay);
    }
}
