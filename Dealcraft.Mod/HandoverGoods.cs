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
    /// <param name="mayCut">
    /// Leave to open one package that holds more than the order and send a
    /// portion out of it — <b>the one thing here the game does not let a player
    /// do</b>, off by default, and tried only once packing as the game allows has
    /// already overshot. A bag that covers the order without overshooting is
    /// packed exactly as it would be with this false.
    /// </param>
    /// <param name="unitsTaken">
    /// What actually left the bag. It is not the order: packages cannot be split
    /// the ordinary way, so a delivery overshoots often enough that the caller
    /// reports this figure rather than the planned one.
    /// </param>
    /// <param name="cut">
    /// What the cut came to, said in packagings and places, or null when no package
    /// was opened. Null covers both "it did not need to be" and "it could not be";
    /// <paramref name="whyNotCut"/> tells those apart.
    /// <b>It names the figures rather than summarising them</b>, because how many
    /// places in the bag a cut costs is the thing the owner is counting and the
    /// slot limit is a number nobody here should be guessing at.
    /// </param>
    /// <param name="whyNotCut">
    /// Why a cut worth making did not happen, or null when none was wanted. There
    /// is more than one reason and they are not the same problem: a bag with no
    /// free slot is something the player can fix by emptying one, while a
    /// remainder no packaging can make is not. Guessing at which would be the mod
    /// telling the player something it does not know.
    /// </param>
    public static Il2CppSystem.Collections.Generic.List<ItemInstance> Take(
        string productId,
        int quality,
        int units,
        bool mayCut,
        out int unitsTaken,
        out string cut,
        out string whyNotCut)
    {
        var taken = new Il2CppSystem.Collections.Generic.List<ItemInstance>();
        unitsTaken = 0;
        cut = null;
        whyNotCut = null;

        List<CarriedProduct> candidates = OfGrade(productId, quality);
        IReadOnlyList<PackageLot> lots = Lots(candidates);
        PackageFill fill = PackagePlan.Fill(lots, units);

        // The same condition the plan used, and only overshoot, because only
        // overshoot can reach here: a package worth opening covers the order on
        // its own, so a bag the ordinary fill cannot cover holds nothing to cut.
        if (mayCut && fill.Covers && fill.Units > units)
        {
            Il2CppSystem.Collections.Generic.List<ItemInstance> portion =
                Cut(candidates, lots, units, out unitsTaken, out cut, out whyNotCut);

            if (portion.Count > 0)
            {
                return portion;
            }

            cut = null;

            // The cut was worth making and could not be carried out. The whole
            // packages go instead, which is what would have happened with the
            // setting off, and the caller has the reason to say out loud — there
            // is more than one, and guessing at which would be the mod telling
            // the player something it does not know.
            unitsTaken = 0;
        }

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
    /// Cut <paramref name="units"/> out of one package that holds more, and put
    /// the remainder back in the bag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One package, not several: the point is a contract that asks for less than
    /// a package holds, and cutting two would be inventing a second thing the
    /// game does not do. The smallest package that holds more than the order is
    /// the one opened, so a brick is left alone while a jar will do — and the
    /// choice is <see cref="PortionCut.Opened"/>, the same call the plan made, so
    /// the two cannot pick different packages out of the same bag.
    /// </para>
    /// <para>
    /// <b>Nothing is created and nothing is lost.</b> One package leaves the
    /// slot, what the contract needs goes across in the smallest packaging the
    /// product allows, and the remainder goes back in the same packaging. Where
    /// the remainder goes is the part that has to be right: a slot holding that
    /// one package empties itself as it leaves and takes the remainder back,
    /// while a slot holding several keeps them and the remainder needs an empty
    /// slot of its own. Without one, nothing is touched at all and the caller
    /// hands over whole packages instead.
    /// </para>
    /// </remarks>
    private static Il2CppSystem.Collections.Generic.List<ItemInstance> Cut(
        List<CarriedProduct> candidates,
        IReadOnlyList<PackageLot> lots,
        int units,
        out int unitsTaken,
        out string said,
        out string why)
    {
        var taken = new Il2CppSystem.Collections.Generic.List<ItemInstance>();
        unitsTaken = 0;
        said = null;

        PlannedCut plan = PlanCut(candidates, lots, units, out why);
        if (!plan.Possible)
        {
            return taken;
        }

        // Both sides of the cut, made before anything is disturbed, so a failure
        // here leaves the bag exactly as it was.
        ProductItemInstance going = plan.Chosen.Product.GetCopy(plan.Split.Portions)
            ?.TryCast<ProductItemInstance>();

        var rest = new List<ProductItemInstance>(plan.Split.Leftover.Count);

        for (int i = 0; i < plan.Split.Leftover.Count; i++)
        {
            ProductItemInstance stack =
                Stack(plan.Chosen.Product, plan.Split.Leftover[i], plan.Packagings);

            if (stack is null)
            {
                why = "the game would not copy the package";
                return taken;
            }

            rest.Add(stack);
        }

        if (going is null)
        {
            why = "the game would not copy the package";
            return taken;
        }

        going.SetPackaging(plan.Portion);

        // Every slot is written once, and that is the rule this block exists to
        // keep.
        //
        // ItemSlot.ChangeQuantity does not change a slot that has an owner — the
        // player's inventory does. It works out the new count from what the slot
        // holds locally and asks the owner to set it, and at zero it asks the owner
        // to clear the slot. Whether the local slot has caught up by the next line
        // is not something this code may assume. So a slot decremented and then
        // written again in the same breath could still be read as holding the
        // package that just left: "one brick, plus seventeen" would go to the owner
        // as eighteen bricks, three hundred and forty units that never existed.
        //
        // The one place that could happen is the slot the package came out of,
        // when that package was alone in it and the remainder goes back there. That
        // slot is not decremented and then refilled; it is given the remainder in a
        // single write, which replaces the one package it held. SetStoredItem
        // replacing a slot's contents is exactly the behaviour that destroyed
        // nineteen jars once — and it is safe here and only here, because the plan
        // offers this slot only when it holds that single package.
        int back = -1;
        for (int i = 0; i < plan.Rooms.Count; i++)
        {
            if (plan.Rooms[i].Pointer == plan.Chosen.Slot.Pointer)
            {
                back = i;
            }
        }

        if (back < 0)
        {
            plan.Chosen.Slot.ChangeQuantity(-1, _internal: false);
        }

        for (int i = 0; i < rest.Count; i++)
        {
            try
            {
                if (i == back)
                {
                    plan.Rooms[i].SetStoredItem(rest[i], _internal: false);
                }
                else if (plan.Rooms[i].ItemInstance is null)
                {
                    plan.Rooms[i].SetStoredItem(rest[i], _internal: false);
                }
                else
                {
                    plan.Rooms[i].ChangeQuantity(rest[i].Quantity, _internal: false);
                }
            }
            catch (Exception error)
            {
                // The package is open and part of its contents is away. Throwing
                // rather than returning empty, and that is the point: the caller's
                // fallback is to hand over whole packages using a plan made before
                // any of this, and the bag it was made against no longer exists.
                // Acting on it would take from a slot that has changed.
                //
                // So the handover is abandoned instead. The caller puts back what
                // it was given, says so, and leaves the contract to the player.
                throw new InvalidOperationException(
                    $"a package was opened and the remainder would not go back in "
                        + $"the bag ({error.Message}); what came off it is in your "
                        + "inventory",
                    error);
            }
        }

        _lastCut[CutKey(plan.Chosen.Product)] = UnityEngine.Time.realtimeSinceStartup;

        taken.Add(going);
        unitsTaken = units;
        why = null;
        said = Said(plan);

        return taken;
    }

    /// <summary>
    /// What a cut came to, in the words and figures a player can check: what was
    /// opened, what went out, what came back and how many places in the bag it
    /// took.
    /// </summary>
    /// <remarks>
    /// The slot limit is in there deliberately. Every reckoning about how many
    /// places a cut costs turns on it, this code has never assumed it, and a
    /// session that prints it settles it — which is cheaper than another evening
    /// of anybody's arithmetic.
    /// </remarks>
    private static string Said(PlannedCut plan)
    {
        var rest = new List<string>(plan.Split.Leftover.Count);
        int fresh = 0;

        for (int i = 0; i < plan.Split.Leftover.Count; i++)
        {
            PortionStack stack = plan.Split.Leftover[i];

            rest.Add($"{stack.Packages}x{Named(plan.Packagings, stack.UnitsPerPackage)}");

            if (plan.Rooms[i].ItemInstance is null)
            {
                fresh++;
            }
        }

        string places = fresh == 0
            ? "onto stacks you already had, costing no slot"
            : $"taking {fresh} slot(s)";

        return $"opened 1x{Named(plan.Packagings, PerPackage(plan.Chosen.Product))}, "
            + $"sent {plan.Split.Portions}x{Named(plan.Packagings, plan.Split.PortionUnits)}, "
            + $"put back {string.Join(" + ", rest)} {places} "
            + $"[a slot holds {StackLimit(plan.Chosen.Product)}]";
    }

    /// <summary>The game's own name for the packaging of this size.</summary>
    private static string Named(List<PackagingDefinition> packagings, int units)
    {
        PackagingDefinition packaging = Of(packagings, units);
        string name = packaging == null ? null : packaging.Name;

        return string.IsNullOrEmpty(name) ? $"{units}-unit package" : name;
    }

    /// <summary>
    /// Whether the bag could carry out a cut for this order, and why not when it
    /// could not. <b>Nothing is taken and nothing is moved.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is asked before the contract is claimed, and that is the whole
    /// reason it exists.</b> A claim is never given back while the contract lives,
    /// so everything that can refuse a handover has to refuse it before the claim
    /// or the contract is spent on one attempt. An empty inventory has always
    /// worked that way — the plan says it cannot deliver, nothing is claimed, and
    /// the next sweep tries again once the player has been home.
    /// </para>
    /// <para>
    /// A cut with no room for the remainder is the same kind of problem and the
    /// owner said so: <i>"Das muss doch genauso funktionieren, wie wenn mein
    /// Inventory leer wäre."</i> He is right. The deal sits there, he sees it is
    /// not going out, he empties a slot, and the next sweep completes it — with
    /// hours of the window left, because a contract is not served the instant it
    /// appears.
    /// </para>
    /// </remarks>
    public static bool WouldCut(string productId, int quality, int units, out string why)
    {
        List<CarriedProduct> candidates = OfGrade(productId, quality);

        return PlanCut(candidates, Lots(candidates), units, out why).Possible;
    }

    /// <summary>
    /// Everything a cut needs decided, with nothing moved: which package is
    /// opened, how both sides are packaged, and which slots the remainder goes
    /// into.
    /// </summary>
    /// <remarks>
    /// One function, asked twice — once by the plan before the contract is claimed
    /// and once by the taking — so "the gate and the act agree" is structural. Two
    /// copies of this is how the original managed to promise a handover it could
    /// not make.
    /// </remarks>
    private static PlannedCut PlanCut(
        List<CarriedProduct> candidates,
        IReadOnlyList<PackageLot> lots,
        int units,
        out string why)
    {
        // One cut at a time, with a moment between them. A cut raises the count
        // on a stack the player already carries, and the count it raises from is
        // what the slot holds locally; the game applies the change through the
        // slot's owner and this code does not assume the local slot has caught up
        // by the next contract. Two cuts of the same product in the same sweep
        // would both start from the old count, and the second would put back the
        // first one's baggies as if they had never arrived.
        //
        // Per product and grade, because that is what shares a stack: the two
        // lines of one contract are different products on different stacks and
        // both may be cut in the same handover.
        //
        // Asked here, in the plan, so the second contract waits for the next
        // sweep rather than being claimed and then refused.
        int at = PortionCut.Opened(lots, units);
        if (at < 0)
        {
            why = "nothing in your bag was worth opening";
            return default;
        }

        CarriedProduct chosen = candidates[at];

        if (_lastCut.TryGetValue(CutKey(chosen.Product), out float then)
            && UnityEngine.Time.realtimeSinceStartup - then < SecondsBetweenCuts)
        {
            why = "a package of it was opened a moment ago; the next sweep takes this one";
            return default;
        }

        List<PackagingDefinition> packagings = Packagings(chosen.Product);
        if (packagings.Count == 0)
        {
            why = "the game lists no packaging for it";
            return default;
        }

        var sizes = new List<int>(packagings.Count);
        for (int i = 0; i < packagings.Count; i++)
        {
            sizes.Add(packagings[i].Quantity);
        }

        int limit = StackLimit(chosen.Product);

        PortionSplit split = PortionCut.Split(
            chosen.Product.Quantity,
            PerPackage(chosen.Product),
            units,
            sizes,

            limit,

            // How much room there already is beside the player's own stacks of
            // this very product, one figure per packaging. Without it the choice
            // of packaging is made blind, and the owner counted what that costs
            // before the code did: "Ich brauche pro Produkt immer drei Plätze."
            RoomBeside(chosen.Product, packagings));

        if (!split.Possible)
        {
            int over = PerPackage(chosen.Product) - units;

            why = $"{units} does not go into a single packaging, or the {over} left "
                + "over will not go back in at all";
            return default;
        }

        PackagingDefinition portion = Of(packagings, split.PortionUnits);
        if (portion == null)
        {
            why = "the packaging it needs is not one the game lists";
            return default;
        }

        // Where every stack of the remainder goes, settled before the package
        // moves.
        //
        // The first version put it back into the source slot unconditionally and
        // used SetStoredItem to do it. That replaces a slot's contents rather than
        // adding to them, so a slot holding more of the same lost all of it:
        // twenty jars against an order of three left one jar and ninety-five units
        // stopped existing. The rollback written to undo a failed cut did the same
        // thing, in exactly the case where it mattered. There is no rollback now,
        // because nothing is replaced — a slot is only ever written when it is
        // known to be empty, and the source slot is only ever reduced by one
        // package.
        var rooms = new List<ItemSlot>(split.Leftover.Count);

        // The opened package was alone in its slot, so that slot empties itself as
        // the package leaves and is a place the remainder can use. Offered rather
        // than assigned: a stack that fits beside its own kind costs no slot at
        // all, and spending this one before trying that would leave the bag
        // fuller than it needs to be.
        ItemSlot emptying = split.SlotsNeeded < split.Leftover.Count ? chosen.Slot : null;

        if (!Room(rooms, chosen.Product, split, packagings, emptying, out why))
        {
            return default;
        }

        why = null;
        return new PlannedCut(chosen, split, packagings, portion, rooms);
    }

    /// <summary>
    /// Find somewhere for every stack of the remainder that does not have one
    /// yet, appending to <paramref name="rooms"/>. False when the bag has no
    /// room.
    /// </summary>
    /// <param name="why">
    /// What was missing, for the player. A free slot and a packaging are not the
    /// same problem: one of them they can fix by emptying a slot.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Onto what is already there first, and an empty slot only if nothing
    /// matches.</b> This asked for an empty slot and nothing else, and the owner's
    /// session of 2026-09-26 showed what that costs: two cuts succeeded and ate
    /// his two free slots, and the next three contracts — for 2, 2 and 6 units —
    /// each took a whole brick, sixty units for ten. He carries six product slots
    /// and two for a bat and a skateboard, so there is no spare one to keep free.
    /// </para>
    /// <para>
    /// And the room was there all along. The first cut had put jars of that exact
    /// product and grade in his bag; the second cut's jars would have stacked onto
    /// them and needed nothing new. So the game's own
    /// <c>ItemInstance.CanStackWith</c> is asked before any slot is taken, and a
    /// stack that fits beside its own kind costs no slot at all.
    /// </para>
    /// <para>
    /// Empty, when it does come to that, and never merely "able to take it": a
    /// slot with something in it that does <em>not</em> stack is a slot whose
    /// contents can be replaced by accident, and that is the defect this exists to
    /// make impossible. Beyond that the game's own questions are asked rather than
    /// assumed — a slot may be locked, may refuse to be added to, may filter the
    /// item out, and may not have the room for that many.
    /// </para>
    /// </remarks>
    private static bool Room(
        List<ItemSlot> rooms,
        ProductItemInstance product,
        PortionSplit split,
        List<PackagingDefinition> packagings,
        ItemSlot emptying,
        out string why)
    {
        why = null;

        PlayerInventory inventory = PlayerSingleton<PlayerInventory>.Instance;
        if (inventory == null)
        {
            why = "the game would not say what you are carrying";
            return false;
        }

        Il2CppSystem.Collections.Generic.List<ItemSlot> slots = inventory.GetAllInventorySlots();
        if (slots is null)
        {
            why = "the game would not say what you are carrying";
            return false;
        }

        while (rooms.Count < split.Leftover.Count)
        {
            PortionStack stack = split.Leftover[rooms.Count];

            // What that stack will be, so the slot is asked about the actual item
            // rather than about the one that is leaving.
            ItemInstance want = Stack(product, stack, packagings);
            if (want is null)
            {
                why = "the packaging the rest needs is not one the game lists";
                return false;
            }

            // Beside its own kind first, then a slot that is already empty, and
            // only then the slot the opened package is leaving.
            ItemSlot found = Beside(slots, rooms, want, stack.Packages)
                ?? Free(slots, rooms, want, stack.Packages)
                ?? Emptying(emptying, rooms, want, stack.Packages);

            if (found is null)
            {
                int wanted = split.Leftover.Count - rooms.Count;

                why = $"nowhere to put the {wanted} stack(s) left over - nothing of the "
                    + "same packaging to add them to, and no free slot";
                return false;
            }

            rooms.Add(found);
        }

        return true;
    }

    /// <summary>
    /// A slot already holding this very thing, with room for that many more, or
    /// null.
    /// </summary>
    /// <remarks>
    /// <b>The answer to "my inventory is always full".</b> Nothing is replaced and
    /// no slot is spent: the count on a stack of the same product, grade and
    /// packaging simply goes up, which is what the game does when a player picks
    /// another baggie off the bench. <c>CanStackWith</c> is the game's own test for
    /// "the same thing", asked rather than reimplemented — quantities excluded,
    /// because how many are there is what <c>GetCapacityForItem</c> answers.
    /// </remarks>
    private static ItemSlot Beside(
        Il2CppSystem.Collections.Generic.List<ItemSlot> slots,
        List<ItemSlot> rooms,
        ItemInstance want,
        int packages)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            ItemSlot slot = slots[i];

            if (slot is null || Already(rooms, slot) || slot.IsLocked || slot.IsAddLocked)
            {
                continue;
            }

            ItemInstance held = slot.ItemInstance;
            if (held is null || !held.CanStackWith(want, false))
            {
                continue;
            }

            if (slot.GetCapacityForItem(want, false) < packages)
            {
                continue;
            }

            return slot;
        }

        return null;
    }

    /// <summary>
    /// The slot the opened package is leaving, if it is free to use and not
    /// already taken.
    /// </summary>
    /// <remarks>
    /// Offered last. It is empty only because the package is about to leave it, so
    /// spending it is spending the one slot the cut itself frees — worth doing when
    /// nothing else will hold the stack, and worth not doing before that.
    /// </remarks>
    private static ItemSlot Emptying(
        ItemSlot emptying,
        List<ItemSlot> rooms,
        ItemInstance want,
        int packages)
    {
        if (emptying is null || Already(rooms, emptying))
        {
            return null;
        }

        // Asked of the item that will be there, not of the package leaving: the
        // slot is about to be empty, so its capacity for this is the item's own
        // stack limit.
        return want.StackLimit >= packages ? emptying : null;
    }

    /// <summary>An empty slot that will take this, or null.</summary>
    private static ItemSlot Free(
        Il2CppSystem.Collections.Generic.List<ItemSlot> slots,
        List<ItemSlot> rooms,
        ItemInstance want,
        int packages)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            ItemSlot slot = slots[i];

            if (slot is null || Already(rooms, slot) || slot.ItemInstance is not null)
            {
                continue;
            }

            if (slot.IsLocked || slot.IsAddLocked || !slot.DoesItemMatchHardFilters(want))
            {
                continue;
            }

            if (slot.GetCapacityForItem(want, false) < packages)
            {
                continue;
            }

            return slot;
        }

        return null;
    }

    /// <summary>
    /// How many more packages of each packaging the player's existing stacks of
    /// this product will take, one figure per entry of
    /// <paramref name="packagings"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The figure that decides whether a cut costs a place in the bag.</b> A
    /// remainder that fits beside its own kind costs none; one that does not costs
    /// a slot, and the owner has nine of them with a bat and a skateboard in two.
    /// </para>
    /// <para>
    /// The game's own two questions, asked rather than reimplemented:
    /// <c>CanStackWith</c> for "the same thing" and <c>GetCapacityForItem</c> for
    /// "how many more". The slot the package is being taken out of is counted too
    /// — it is the same product, and a remainder in the same packaging as what it
    /// came from would simply go back on that pile.
    /// </para>
    /// </remarks>
    private static List<int> RoomBeside(
        ProductItemInstance product,
        List<PackagingDefinition> packagings)
    {
        var room = new List<int>(packagings.Count);

        PlayerInventory inventory = PlayerSingleton<PlayerInventory>.Instance;
        Il2CppSystem.Collections.Generic.List<ItemSlot> slots =
            inventory == null ? null : inventory.GetAllInventorySlots();

        for (int i = 0; i < packagings.Count; i++)
        {
            room.Add(0);
        }

        if (slots is null)
        {
            return room;
        }

        for (int i = 0; i < packagings.Count; i++)
        {
            ProductItemInstance want = Packaged(product, packagings[i], 1);
            if (want is null)
            {
                continue;
            }

            // The most any ONE stack will take, not the total across them. The
            // remainder goes onto a single stack, so two stacks with ten free each
            // are room for ten, not twenty - summing them promised a place that
            // Beside then could not find.
            int free = 0;

            for (int at = 0; at < slots.Count; at++)
            {
                ItemSlot slot = slots[at];

                if (slot is null || slot.IsLocked || slot.IsAddLocked)
                {
                    continue;
                }

                ItemInstance held = slot.ItemInstance;
                if (held is null || !held.CanStackWith(want, false))
                {
                    continue;
                }

                free = Math.Max(free, slot.GetCapacityForItem(want, false));
            }

            room[i] = free;
        }

        return room;
    }

    /// <summary>One stack of the remainder as the item it will be.</summary>
    private static ProductItemInstance Stack(
        ProductItemInstance product,
        PortionStack stack,
        List<PackagingDefinition> packagings) =>
        Packaged(product, Of(packagings, stack.UnitsPerPackage), stack.Packages);

    /// <summary>
    /// This product, this packaging, this many packages — the item the game would
    /// hold, built without touching anything.
    /// </summary>
    private static ProductItemInstance Packaged(
        ProductItemInstance product,
        PackagingDefinition packaging,
        int packages)
    {
        if (packaging == null || packages <= 0)
        {
            return null;
        }

        ProductItemInstance copy = product.GetCopy(packages)?.TryCast<ProductItemInstance>();
        if (copy is null)
        {
            return null;
        }

        copy.SetPackaging(packaging);
        return copy;
    }

    /// <summary>
    /// Real seconds between two cuts. See <see cref="PlanCut"/>: long enough for
    /// the game to have applied the last one before the next reads the same
    /// stack, short against a handover sweep, which waits longer than this anyway.
    /// </summary>
    private const float SecondsBetweenCuts = 1f;

    /// <summary>
    /// When a package of each product and grade was last opened, in real seconds
    /// since start.
    /// </summary>
    private static readonly Dictionary<string, float> _lastCut = new();

    /// <summary>What shares a stack: the product and its grade.</summary>
    private static string CutKey(ProductItemInstance product)
    {
        ItemDefinition definition = product.Definition;
        string id = definition == null ? string.Empty : definition.ID;

        return $"{id}|{(int)product.Quality}";
    }

    /// <summary>
    /// A cut worked out and not yet made: the package to open, how both sides are
    /// packaged, and a slot for every stack of the remainder.
    /// </summary>
    private readonly struct PlannedCut
    {
        internal PlannedCut(
            CarriedProduct chosen,
            PortionSplit split,
            List<PackagingDefinition> packagings,
            PackagingDefinition portion,
            List<ItemSlot> rooms)
        {
            Chosen = chosen;
            Split = split;
            Packagings = packagings;
            Portion = portion;
            Rooms = rooms;
        }

        /// <summary>Whether the bag can carry this cut out.</summary>
        public bool Possible => Rooms is not null;

        public CarriedProduct Chosen { get; }

        public PortionSplit Split { get; }

        public List<PackagingDefinition> Packagings { get; }

        /// <summary>The packaging the portion goes to the customer in.</summary>
        public PackagingDefinition Portion { get; }

        /// <summary>
        /// One slot per stack of the remainder, in the remainder's own order, each
        /// checked empty and able to hold it.
        /// </summary>
        public List<ItemSlot> Rooms { get; }
    }

    /// <summary>Whether this slot has already been set aside for another stack.</summary>
    private static bool Already(List<ItemSlot> rooms, ItemSlot slot)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (ReferenceEquals(rooms[i], slot) || rooms[i].Pointer == slot.Pointer)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Every packaging the game allows for this product, smallest first.
    /// </summary>
    /// <remarks>
    /// All of them rather than the smallest, because the smallest is not always
    /// the right one: seventeen units back in baggies is more packages than a
    /// slot will hold, while three jars and two baggies is the same product in
    /// two stacks. <see cref="PortionCut"/> picks; this only reports what the
    /// game offers.
    /// </remarks>
    private static List<PackagingDefinition> Packagings(ProductItemInstance product)
    {
        var found = new List<PackagingDefinition>();

        var definition = product.Definition.TryCast<ProductDefinition>();
        if (definition is null)
        {
            return found;
        }

        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PackagingDefinition> valid =
            definition.ValidPackaging;

        if (valid is null)
        {
            return found;
        }

        for (int i = 0; i < valid.Length; i++)
        {
            PackagingDefinition candidate = valid[i];

            if (candidate != null && candidate.Quantity > 0)
            {
                found.Add(candidate);
            }
        }

        found.Sort((left, right) => left.Quantity.CompareTo(right.Quantity));
        return found;
    }

    /// <summary>The packaging holding this many units, or null.</summary>
    private static PackagingDefinition Of(List<PackagingDefinition> packagings, int units)
    {
        for (int i = 0; i < packagings.Count; i++)
        {
            if (packagings[i].Quantity == units)
            {
                return packagings[i];
            }
        }

        return null;
    }

    /// <summary>
    /// How many packages one inventory slot will hold for this product, or zero
    /// when the game will not say.
    /// </summary>
    /// <remarks>
    /// The bound that decides whether a cut is possible at all, so it is asked of
    /// the product rather than assumed. The definition's own limit first, the
    /// game's default behind it — a product that answers neither is not cut.
    /// </remarks>
    private static int StackLimit(ProductItemInstance product)
    {
        ItemDefinition definition = product.Definition;
        if (definition == null)
        {
            return 0;
        }

        int limit = definition.StackLimit;

        return limit > 0 ? limit : ItemDefinition.DefaultStackLimit;
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
