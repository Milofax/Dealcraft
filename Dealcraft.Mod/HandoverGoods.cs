using System;
using System.Collections.Generic;
using System.Globalization;
using Dealcraft.Core;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Product.Packaging;

namespace Dealcraft;

/// <summary>
/// The goods themselves: what is in reach, and taking it out of the host's
/// hands so it can be handed over.
///
/// "In reach" is read narrowly and deliberately — the host player's own
/// inventory slots, which is what a player standing in front of a customer can
/// actually give them. Storage, vehicles and the rest are not swept.
///
/// Nothing here decides anything; <see cref="GradeChoice"/> does. This reads the
/// game and moves items, and it is too thin to test without a running game.
/// </summary>
internal static class HandoverGoods
{
    /// <summary>
    /// How many units of each grade of <paramref name="productId"/> the host is
    /// carrying. Only packaged product counts: the game's own handover screen
    /// refuses loose product, so offering it would be offering something the
    /// game will not take.
    /// </summary>
    public static IReadOnlyList<GradeStock> InReach(string productId)
    {
        var byGrade = new Dictionary<int, int>();

        foreach (CarriedProduct carried in Carried(productId))
        {
            int quality = (int)carried.Product.Quality;
            byGrade.TryGetValue(quality, out int units);
            byGrade[quality] = units + carried.Product.GetTotalAmount();
        }

        var stock = new List<GradeStock>(byGrade.Count);
        foreach (KeyValuePair<int, int> grade in byGrade)
        {
            stock.Add(new GradeStock(grade.Key, grade.Value));
        }

        return stock;
    }

    /// <summary>
    /// Every package of <paramref name="productId"/> the player is carrying,
    /// with the grade each one holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="InReach"/> answers in units, and units are not what leaves the
    /// inventory: packages are. A grade can hold the units for an order and be
    /// unable to deliver them, because a handover holds
    /// <see cref="PackagePlan.MostPositions"/> stacks — so the grade and the
    /// packaging have to be decided together, and this is the reading that lets
    /// them be.
    /// </para>
    /// <para>
    /// Ordered by grade and then by package size, largest first, which is the
    /// order <see cref="Take"/> walks the slots of one grade in. The two agree
    /// by construction rather than by coincidence: a plan made from this list
    /// and the goods <see cref="Take"/> then removes come out of the same
    /// deterministic choice.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<CarriedLot> LotsInReach(string productId)
    {
        var lots = new List<CarriedLot>();

        foreach (CarriedProduct carried in Carried(productId))
        {
            lots.Add(new CarriedLot(
                (int)carried.Product.Quality,
                PerPackage(carried.Product),
                carried.Product.Quantity));
        }

        lots.Sort(static (left, right) =>
        {
            int by = left.Quality.CompareTo(right.Quality);
            return by != 0 ? by : right.UnitsPerPackage.CompareTo(left.UnitsPerPackage);
        });

        return lots;
    }

    /// <summary>
    /// Every product the player is carrying any packaged amount of, as the game
    /// IDs them.
    /// </summary>
    /// <remarks>
    /// What the stock reading is a list of. It is the player's own pockets and
    /// nothing else — see <see cref="StockReading"/> for the two wider readings
    /// and why neither is taken.
    /// </remarks>
    public static IReadOnlyList<string> ProductsInReach()
    {
        var products = new List<string>();

        foreach (CarriedProduct carried in Carried(productId: null))
        {
            ItemDefinition definition = carried.Product.Definition;
            string id = definition == null ? null : definition.ID;

            if (!string.IsNullOrEmpty(id) && !Named(products, id))
            {
                products.Add(id);
            }
        }

        return products;
    }

    /// <summary>Whether this product is already in the list, however it is cased.</summary>
    private static bool Named(List<string> products, string productId)
    {
        foreach (string already in products)
        {
            if (string.Equals(already, productId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Take the least product of <paramref name="quality"/> that covers
    /// <paramref name="units"/> out of the host's inventory, and hand it back as
    /// the items to give the customer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Which packages that is, <see cref="PackagePlan"/> decides. This used to
    /// walk the slots smallest-package-first and call it least overshoot; it is
    /// not, and it cost real product — the arithmetic is on that type.
    /// </para>
    /// <para>
    /// Nothing is taken at all when the order cannot be covered. A short
    /// delivery is refused by the game anyway, and taking goods for one would
    /// mean putting them back.
    /// </para>
    /// <para>
    /// The caller owns the result. If the handover does not happen it must put
    /// them back with <see cref="Return"/>, or the host has given away goods
    /// for a deal that never occurred.
    /// </para>
    /// </remarks>
    public static Il2CppSystem.Collections.Generic.List<ItemInstance> Take(
        string productId,
        int quality,
        int units,
        out int unitsTaken)
    {
        var taken = new Il2CppSystem.Collections.Generic.List<ItemInstance>();
        unitsTaken = 0;

        List<CarriedProduct> candidates = OfGrade(productId, quality);
        PackageFill fill = PackagePlan.Fill(Lots(candidates), units);

        if (!fill.Covers)
        {
            return taken;
        }

        for (int at = 0; at < candidates.Count; at++)
        {
            int items = fill.From(at);
            if (items <= 0)
            {
                continue;
            }

            CarriedProduct carried = candidates[at];
            ProductItemInstance product = carried.Product;

            // A copy leaves the original free to be reduced by the same amount,
            // which is what dragging part of a stack into the handover screen
            // does. The reduction goes through the slot rather than the item:
            // read, that is the branch that tells the slot's
            // owner anything changed, and the same method clears the slot by
            // itself when the count reaches zero.
            ItemInstance copy = product.GetCopy(items);
            if (copy is null)
            {
                continue;
            }

            carried.Slot.ChangeQuantity(-items, _internal: false);
            taken.Add(copy);
            unitsTaken += items * PerPackage(product);
        }

        return taken;
    }

    /// <summary>
    /// What the handover would come to without taking anything: the units, the
    /// packages and why there is no delivery when there is none.
    /// </summary>
    /// <remarks>
    /// The same call <see cref="Take"/> makes, on the same bag. It is a separate
    /// method rather than a flag on that one because <see cref="Take"/> mutates
    /// the player's inventory, and a method that sometimes does and sometimes
    /// does not is a method somebody will call wrongly.
    /// </remarks>
    public static PackageFill WouldFill(string productId, int quality, int units) =>
        PackagePlan.Fill(Lots(OfGrade(productId, quality)), units);

    /// <summary>
    /// What <see cref="Take"/> would use, named, without taking anything: "2
    /// jars", "1 brick and 2 baggies". Empty when nothing would be taken.
    /// </summary>
    /// <remarks>
    /// It exists because the packaging is the only lever a player has. The
    /// delivery is already the least that covers the order, so naming what it
    /// used is what lets them carry something else next time. Vocabulary:
    /// packaging, never bundle.
    /// </remarks>
    public static string WouldTake(string productId, int quality, int units)
    {
        List<CarriedProduct> candidates = OfGrade(productId, quality);
        PackageFill fill = PackagePlan.Fill(Lots(candidates), units);

        var used = new List<KeyValuePair<string, int>>();

        for (int at = 0; at < candidates.Count; at++)
        {
            int items = fill.From(at);
            if (items > 0)
            {
                Count(used, Named(candidates[at].Product), items);
            }
        }

        return Spell(used);
    }

    /// <summary>
    /// The slots holding this product at this grade, in a fixed order, so that
    /// the plan's answer can be read back against them by position.
    /// </summary>
    private static List<CarriedProduct> OfGrade(string productId, int quality)
    {
        var mine = new List<CarriedProduct>();

        foreach (CarriedProduct carried in Carried(productId))
        {
            if ((int)carried.Product.Quality == quality)
            {
                mine.Add(carried);
            }
        }

        // Largest package first, so that where several combinations tie the one
        // the plan returns is spelled the same way every time.
        mine.Sort((left, right) => PerPackage(right.Product).CompareTo(PerPackage(left.Product)));
        return mine;
    }

    /// <summary>The slots as the plan reads them: package size and how many.</summary>
    private static IReadOnlyList<PackageLot> Lots(List<CarriedProduct> candidates)
    {
        var lots = new List<PackageLot>(candidates.Count);

        foreach (CarriedProduct carried in candidates)
        {
            lots.Add(new PackageLot(PerPackage(carried.Product), carried.Product.Quantity));
        }

        return lots;
    }

    /// <summary>
    /// Units in one package. A product with no amount recorded is one unit, so
    /// that a broken definition cannot divide by zero.
    /// </summary>
    private static int PerPackage(ProductItemInstance product) =>
        product.Amount > 0 ? product.Amount : 1;

    /// <summary>What the game calls this package, in the singular.</summary>
    private static string Named(ProductItemInstance product)
    {
        PackagingDefinition packaging = product.AppliedPackaging;
        string name = packaging == null ? null : packaging.Name;

        return string.IsNullOrWhiteSpace(name) ? "package" : name.Trim().ToLowerInvariant();
    }

    private static void Count(List<KeyValuePair<string, int>> used, string name, int items)
    {
        for (int i = 0; i < used.Count; i++)
        {
            if (string.Equals(used[i].Key, name, StringComparison.Ordinal))
            {
                used[i] = new KeyValuePair<string, int>(name, used[i].Value + items);
                return;
            }
        }

        used.Add(new KeyValuePair<string, int>(name, items));
    }

    /// <summary>
    /// "2 jars", or "1 brick and 2 baggies" where more than one kind is used.
    /// Plural by adding an s, which is what the game's own package names take.
    /// </summary>
    private static string Spell(List<KeyValuePair<string, int>> used)
    {
        if (used.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(used.Count);
        foreach (KeyValuePair<string, int> one in used)
        {
            string name = one.Value == 1 ? one.Key : one.Key + "s";
            parts.Add($"{one.Value.ToString(CultureInfo.InvariantCulture)} {name}");
        }

        return parts.Count == 1
            ? parts[0]
            : string.Join(" and ", parts);
    }

    /// <summary>
    /// Put goods back. Called when the handover could not be completed after
    /// the items were already taken, so that a failure costs nothing.
    /// </summary>
    public static void Return(Il2CppSystem.Collections.Generic.List<ItemInstance> items)
    {
        if (items is null || !PlayerSingleton<PlayerInventory>.InstanceExists)
        {
            return;
        }

        PlayerInventory inventory = PlayerSingleton<PlayerInventory>.Instance;
        if (inventory == null)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ItemInstance item = items[i];
            if (item is not null)
            {
                inventory.AddItemToInventory(item);
            }
        }
    }

    /// <summary>One packaged product, and the slot it is sitting in.</summary>
    private readonly struct CarriedProduct
    {
        public CarriedProduct(ItemSlot slot, ProductItemInstance product)
        {
            Slot = slot;
            Product = product;
        }

        public ItemSlot Slot { get; }

        public ProductItemInstance Product { get; }
    }

    /// <summary>
    /// Every packaged instance of this product the host is carrying. Empty when
    /// there is no local player, which is the ordinary state at the main menu.
    /// </summary>
    /// <param name="productId">
    /// The product to look for, or null for every product in the inventory —
    /// which is what <see cref="ProductsInReach"/> asks for, and the only caller
    /// that does.
    /// </param>
    private static List<CarriedProduct> Carried(string productId)
    {
        var carried = new List<CarriedProduct>();

        if (!PlayerSingleton<PlayerInventory>.InstanceExists)
        {
            return carried;
        }

        bool anyProduct = string.IsNullOrEmpty(productId);

        PlayerInventory inventory = PlayerSingleton<PlayerInventory>.Instance;
        if (inventory == null)
        {
            return carried;
        }

        Il2CppSystem.Collections.Generic.List<ItemSlot> slots = inventory.GetAllInventorySlots();
        if (slots is null)
        {
            return carried;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            ItemSlot slot = slots[i];
            if (slot is null || slot.IsRemovalLocked || slot.IsLocked)
            {
                continue;
            }

            ItemInstance item = slot.ItemInstance;
            if (item is null || item.Quantity <= 0)
            {
                continue;
            }

            ProductItemInstance product = item.TryCast<ProductItemInstance>();
            if (product is null || product.AppliedPackaging == null)
            {
                continue;
            }

            ItemDefinition definition = product.Definition;
            if (definition == null)
            {
                continue;
            }

            if (!anyProduct && !string.Equals(definition.ID, productId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            carried.Add(new CarriedProduct(slot, product));
        }

        return carried;
    }
}
