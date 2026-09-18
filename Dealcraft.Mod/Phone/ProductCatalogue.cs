using System;
using Dealcraft.Core;
using Il2CppScheduleOne.Product;

namespace Dealcraft.Phone;

/// <summary>
/// How many products the game itself knows about, right now.
/// </summary>
/// <remarks>
/// <para>
/// These two lists are what the Product Manager app draws. Its <c>Start</c>
/// (RVA <c>0x9EC220</c>) walks <c>ProductManager.FavouritedProducts</c> — the
/// static at <c>+0x10</c>, iterated at <c>0x1809ec5ac</c> — calling
/// <c>CreateFavouriteEntry</c>, and then <c>ProductManager.DiscoveredProducts</c>
/// — the static at <c>+0x0</c>, iterated at <c>0x1809ec682</c> — calling
/// <c>CreateEntry</c>. It also subscribes to the events that add to them later,
/// so the app's two lists follow these two for as long as the scene lives.
/// </para>
/// <para>
/// Which static is which is read from the other side rather than assumed:
/// <c>RpcLogic___SetProductDiscovered_619441887</c> (RVA <c>0x7FEB50</c>) does
/// its <c>Contains</c> and <c>Add</c> on <c>+0x0</c>, and
/// <c>RpcLogic___SetProductFavourited_619441887</c> (RVA <c>0x7FEDD0</c>) on
/// <c>+0x10</c>.
/// </para>
/// <para>
/// A count that cannot be read comes back as
/// <see cref="ProductListReading.Unread"/> rather than as zero. Zero is a real
/// answer — a game that knows no products — and an app holding rows against it
/// would then read as duplication.
/// </para>
/// </remarks>
internal static class ProductCatalogue
{
    /// <summary>How many products the game has discovered, or -1.</summary>
    public static int Discovered() => Count(() => ProductManager.DiscoveredProducts);

    /// <summary>How many products the game holds as favourites, or -1.</summary>
    public static int Favourited() => Count(() => ProductManager.FavouritedProducts);

    private static int Count(Func<Il2CppSystem.Collections.Generic.List<ProductDefinition>> list)
    {
        try
        {
            Il2CppSystem.Collections.Generic.List<ProductDefinition> read = list();
            return read != null ? read.Count : ProductListReading.Unread;
        }
        catch (Exception)
        {
            // The statics are only there once the class has been initialised,
            // which is a state of the scene rather than a fault. The caller
            // reports an unread count as a check that could not be made.
            return ProductListReading.Unread;
        }
    }
}
