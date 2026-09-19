using System;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;

namespace Dealcraft;

/// <summary>
/// Whether this machine may act on a contract at all, read off FishNet.
/// </summary>
/// <remarks>
/// <para>
/// Two conditions, not one. <c>IsServer</c> is the ownership rule: six players
/// may each have Dealcraft installed and each see the same replicated customer
/// state, and without the gate six machines act on one contract. The client half
/// matters too, because the vanilla calls the mod drives are written by the
/// client and are dropped without one.
/// </para>
/// <para>
/// One reading, here, because three passes used to carry a copy of it and a
/// fourth carried a slightly different copy. A reading that throws answers
/// <see cref="ServerAuthority.NotTheServer"/>: the safe end, where nothing acts.
/// </para>
/// </remarks>
internal static class ServerAuthorityReader
{
    /// <summary>
    /// The session's answer, for a pass deciding whether to run at all.
    /// </summary>
    public static ServerAuthority Read()
    {
        try
        {
            if (!Il2CppFishNet.InstanceFinder.IsServer)
            {
                return Il2CppFishNet.InstanceFinder.IsClient
                    ? ServerAuthority.Guest
                    : ServerAuthority.NotTheServer;
            }

            return Il2CppFishNet.InstanceFinder.IsClient
                ? ServerAuthority.Held
                : ServerAuthority.WithoutAClient;
        }
        catch (Exception)
        {
            return ServerAuthority.NotTheServer;
        }
    }

    /// <summary>
    /// The same question about one customer, for a pass that is about to call
    /// something on them.
    /// </summary>
    /// <remarks>
    /// The finer of the two. <c>IsClientInitialized</c> is the customer's own
    /// network object being ready, which both the accept and the counter-offer
    /// writers test before touching the buffer — tests it first and
    /// its false branch only logs a warning. A session-wide client half being up
    /// does not guarantee this particular NPC's is.
    /// </remarks>
    public static ServerAuthority Read(Customer customer)
    {
        try
        {
            if (!Il2CppFishNet.InstanceFinder.IsServer)
            {
                return customer.IsClientInitialized
                    ? ServerAuthority.Guest
                    : ServerAuthority.NotTheServer;
            }

            return customer.IsClientInitialized
                ? ServerAuthority.Held
                : ServerAuthority.WithoutAClient;
        }
        catch (Exception)
        {
            return ServerAuthority.NotTheServer;
        }
    }
}
