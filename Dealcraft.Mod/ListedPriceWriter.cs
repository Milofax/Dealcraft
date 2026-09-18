using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Product;

namespace Dealcraft;

/// <summary>
/// The one call in this mod that changes the game's own state for everybody.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which call a host may make, and the evidence.</b> <c>CLAUDE.md</c> is
/// absolute: a local field write on the host is not synchronisation. So the
/// price is written through the game's own observers RPC, and this is what was
/// read out of <c>GameAssembly.dll</c> for <c>0.4.6f13</c> before a line of it
/// was built:
/// </para>
/// <list type="number">
/// <item>
/// <c>ProductManager::SetPrice(NetworkConnection, String, Single)</c>, RVA
/// <c>0x804AA0</c>, has two arms and the branch is on <c>conn</c>
/// (<c>test %rdx,%rdx; je 0x180804c06</c> at <c>0x180804abc</c>).
/// </item>
/// <item>
/// <b><c>conn == null</c> is the broadcast.</b> That arm calls the FishNet
/// send stub at <c>0x180563530</c> with RPC hash <c>0x1e</c> — the same stub
/// <c>RpcWriter___Observers_SetPrice_4077118173</c> calls at
/// <c>0x180801c00</c>, which is what identifies it as the observers writer —
/// and then calls <c>RpcLogic___SetPrice_4077118173</c> at <c>0x180804d29</c>
/// so the host applies it too. Every client on the connection gets it, modded
/// or not: an <c>ObserversRpc</c> is delivered by the vanilla reader
/// <c>RpcReader___Observers_SetPrice_4077118173</c>, which every unmodified
/// client already has.
/// </item>
/// <item>
/// <b><c>conn != null</c> is not ours.</b> That arm calls <c>0x180563ae0</c>
/// with hash <c>0x1f</c> — the stub <c>RpcWriter___Target_SetPrice</c> calls at
/// <c>0x180803d8a</c> — and returns without running the logic. One connection,
/// nobody else.
/// </item>
/// <item>
/// <b>It is the game's own server-side path.</b>
/// <c>RpcLogic___SendPrice_606697822</c> (RVA <c>0x7FE810</c>) is thirty-one
/// bytes and its whole body is <c>xor %edx,%edx; call 0x180804aa0</c> — that
/// is <c>SetPrice(null, productID, value)</c>. So when the vanilla Products app
/// on any machine submits a price, the server ends up in exactly the call this
/// class makes. Dealcraft adds no path the game does not already use.
/// </item>
/// <item>
/// <b>Not the server means nothing is written.</b> Both arms are behind
/// <c>NetworkBehaviour::get_IsServerInitialized()</c> (<c>0x180c7ffb0</c>); the
/// failing arm logs through <c>NetworkManager::LogWarning</c> and writes
/// nothing. The switch is host-only for that reason and not only by policy.
/// </item>
/// <item>
/// <b>What lands.</b> <c>RpcLogic___SetPrice</c> (RVA <c>0x7FE9C0</c>) clamps
/// the value between the floats at <c>0x182dffb44</c> (<c>1.0</c>) and
/// <c>0x182e00c18</c> (<c>999.0</c>), rounds it to a whole dollar, and writes
/// <c>ProductPrices</c> at <c>+0x210</c>. A price finer than a dollar is
/// therefore not a price the player could ever see, which is why
/// <see cref="Dealcraft.Core.ListedPriceDecision"/> compares whole dollars.
/// </item>
/// </list>
/// <para>
/// Nothing is added to Schedule I's Products app. The price lives in
/// <c>ProductManager</c> and both of the game's own controls
/// (<c>ProductAppDetailPanel.AdjustPrice</c> and <c>PriceSubmitted</c>) reach it
/// the same way, so the panel is neither patched nor read.
/// </para>
/// </remarks>
internal static class ListedPriceWriter
{
    /// <summary>
    /// Write one product's listed price for everybody, or say why it could not
    /// be written.
    /// </summary>
    /// <returns>Empty when it was written; the reason when it was not.</returns>
    public static string Write(string productId, float price)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return "the product has no id to key a price on";
        }

        if (!NetworkSingleton<ProductManager>.InstanceExists)
        {
            return "there is no product manager yet";
        }

        ProductManager manager = NetworkSingleton<ProductManager>.Instance;
        if (manager == null)
        {
            return "the product manager went away";
        }

        // The null connection is the whole point: it is the observers broadcast,
        // and it is the same call the game makes when a client submits a price.
        manager.SetPrice(null, productId, price);
        return string.Empty;
    }
}
