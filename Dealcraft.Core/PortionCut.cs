using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Opening one package to send a smaller portion out of it: which package is
/// opened, how much goes across, how much comes back, and whether the remainder
/// needs an inventory slot of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one thing in this mod the game does not let a player do.</b> It
/// repackages only at a packaging station, so a brick in a pocket stays a brick.
/// Cutting is the owner's deliberate exception, off by default, and it is tried
/// only once packing as the game allows has already overshot.
/// </para>
/// <para>
/// It lives here, away from the inventory, because all of it is arithmetic and
/// the arithmetic is what went wrong. The first version worked the same sums out
/// inside the code that moves the goods, where the only way to check them was to
/// play the game and count what was left — and what it got wrong was a slot
/// holding more than one package, which is the case nobody thinks to try by
/// hand. Both the plan and the taking now ask this, so "the plan and the take
/// agree about the same bag" is structural rather than a comment on two copies.
/// </para>
/// </remarks>
public static class PortionCut
{
    /// <summary>
    /// Which lot to open, or <c>-1</c> when none can be: the smallest package
    /// that holds more than the order.
    /// </summary>
    /// <remarks>
    /// Smallest, so a brick is left alone while a jar will do. A lot whose
    /// package holds no more than the order is not a candidate — there is
    /// nothing to cut off it whole, and where every lot is like that
    /// <see cref="PackagePlan.Fill(IReadOnlyList{PackageLot}, int, bool)"/> sends
    /// whole packages and opens one for what they leave.
    /// </remarks>
    public static int Opened(IReadOnlyList<PackageLot> lots, int order)
    {
        if (lots is null || order <= 0)
        {
            return -1;
        }

        int at = -1;
        int smallest = int.MaxValue;

        for (int i = 0; i < lots.Count; i++)
        {
            if (lots[i].Packages > 0
                && lots[i].UnitsPerPackage > order
                && lots[i].UnitsPerPackage < smallest)
            {
                smallest = lots[i].UnitsPerPackage;
                at = i;
            }
        }

        return at;
    }

    /// <summary>
    /// Most stacks either side of a cut may come to.
    /// <c>HandoverScreen.CustomerSlotCount</c> is four, and a remainder that
    /// needs more places than the screen has is not a cut worth making.
    /// </summary>
    public const int MostStacks = 4;

    /// <summary>
    /// Every inventory write a cut makes, in the order it makes them: the slots
    /// give up their packages first, and the remainder goes back after.
    /// </summary>
    /// <remarks>
    /// <b>In that order so that a failure is a shortage and not a surplus.</b> A
    /// write can fail halfway through, and <see cref="Out"/> then says what is out
    /// of the bag at that moment. Goods that are out can be put back; a remainder
    /// written before its package left would be product that never existed.
    /// </remarks>
    /// <param name="fill">The fill being carried out.</param>
    /// <param name="stacks">How many stacks the remainder comes to.</param>
    /// <param name="back">
    /// Which stack goes back into the opened package's own slot, replacing all it
    /// held in one write, or <c>-1</c> when none does.
    /// </param>
    public static IReadOnlyList<CutWrite> Writes(PackageFill fill, int stacks, int back)
    {
        var writes = new List<CutWrite>(fill.Lots + stacks);

        for (int lot = 0; lot < fill.Lots; lot++)
        {
            if (fill.From(lot) > 0)
            {
                writes.Add(new CutWrite(lot, fill.From(lot), lot == fill.Opened ? back : -1));
            }
        }

        for (int stack = 0; stack < stacks; stack++)
        {
            if (stack != back)
            {
                writes.Add(new CutWrite(-1, 0, stack));
            }
        }

        return writes;
    }

    /// <summary>
    /// What is out of the bag once the first <paramref name="landed"/> writes of
    /// <paramref name="writes"/> have happened. With all of them that is what the
    /// customer gets; with fewer it is what has to be put back.
    /// </summary>
    /// <param name="writes">The cut's writes, from <see cref="Writes"/>.</param>
    /// <param name="landed">How many of them took effect.</param>
    /// <param name="fill">The fill being carried out.</param>
    /// <param name="emptiedOnly">
    /// The last landed write replaced a slot's contents and stopped halfway: the
    /// slot gave up what it held and the stack never went in. Replacing clears
    /// first, so that state is real, and in it the slot's goods and the stack are
    /// all out.
    /// </param>
    public static CutGoods Out(
        IReadOnlyList<CutWrite> writes, int landed, PackageFill fill, bool emptiedOnly = false)
    {
        var whole = new List<int>();
        var stacks = new List<int>();
        bool portion = false;

        for (int at = 0; at < landed; at++)
        {
            CutWrite write = writes[at];

            if (write.Lot >= 0 && fill.Whole(write.Lot) > 0)
            {
                whole.Add(write.Lot);
            }

            if (write.Lot == fill.Opened)
            {
                portion = true;

                foreach (CutWrite other in writes)
                {
                    if (other.Stack >= 0)
                    {
                        stacks.Add(other.Stack);
                    }
                }
            }

            if (write.Stack >= 0 && !(emptiedOnly && at == landed - 1))
            {
                stacks.Remove(write.Stack);
            }
        }

        return new CutGoods(whole, portion, stacks);
    }

    /// <summary>
    /// What the cut in <paramref name="fill"/> comes to, for the bag it was made
    /// from.
    /// </summary>
    /// <remarks>
    /// <b>The slot is counted after the whole packages have left it.</b> Two jars
    /// in one slot, one sent whole and one opened, leave that slot empty, so the
    /// remainder has it; counted before, the slot looks occupied and the same
    /// handover asks for a free slot it does not need.
    /// </remarks>
    public static PortionSplit Split(
        PackageFill fill,
        IReadOnlyList<PackageLot> lots,
        IReadOnlyList<int> packagings,
        int stackLimit,
        IReadOnlyList<int>? roomBeside)
    {
        if (!fill.Cuts)
        {
            return default;
        }

        PackageLot lot = lots[fill.Opened];

        return Split(
            lot.Packages - fill.Whole(fill.Opened),
            lot.UnitsPerPackage,
            fill.Portion,
            packagings,
            stackLimit,
            roomBeside);
    }

    /// <summary>
    /// What opening one package of a lot comes to: how the portion is packaged,
    /// how the remainder is packaged, and how many free inventory slots putting it
    /// away needs.
    /// </summary>
    /// <param name="packagesInSlot">
    /// How many packages that inventory slot holds. <b>The figure the first
    /// version ignored</b>, and one of the reasons this is a function: one package
    /// leaves the slot empty, so the remainder has that slot to start with, while
    /// several leave it occupied by packages of a different size, which cannot
    /// share a slot.
    /// </param>
    /// <param name="unitsPerPackage">Units in one package of that lot.</param>
    /// <param name="order">
    /// Units to cut: what the contract asks for, less any whole packages sent.
    /// </param>
    /// <param name="packagings">
    /// Units held by each packaging the product allows, in any order — the game's
    /// own three are a baggie at one, a jar at five and a brick at twenty. Both
    /// sides of the cut are packaged out of this list.
    /// </param>
    /// <param name="stackLimit">
    /// How many packages one inventory slot will hold,
    /// <c>ItemDefinition.StackLimit</c>. <b>The other reason this is a
    /// function.</b> A brick is twenty units and a slot holds ten packages, so
    /// cutting three units off one leaves seventeen — and "put the rest back in
    /// baggies" is a rule that silently loses product the moment the rest is
    /// large.
    /// </param>
    public static PortionSplit Split(
        int packagesInSlot,
        int unitsPerPackage,
        int order,
        IReadOnlyList<int> packagings,
        int stackLimit) =>
        Split(
            packagesInSlot,
            unitsPerPackage,
            order,
            packagings,
            stackLimit,
            roomBeside: System.Array.Empty<int>());

    /// <summary>
    /// The same, told how much room there already is beside the player's own
    /// stacks.
    /// </summary>
    /// <param name="roomBeside">
    /// For each entry of <paramref name="packagings"/>, how many more packages of
    /// that size the player's existing stacks of this very product will take. Null
    /// or short means none.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>This is what decides how many places in the bag a cut costs, and it was
    /// got wrong in the way that mattered most.</b> The first version packaged the
    /// remainder in the largest packaging that divided it, on the reasoning that
    /// fewest packages is tidiest. Seventeen units came back as three jars and two
    /// baggies — two new stacks — and the owner counted the real cost before the
    /// code did: <i>"Ich brauche pro Produkt immer drei Plätze."</i> Bricks, jars
    /// and baggies, times every product he carries, in nine slots of which two hold
    /// a bat and a skateboard.
    /// </para>
    /// <para>
    /// Fewest <em>stacks</em> is the thing to minimise, not fewest packages, because
    /// a stack is a place in the bag and a package is not. Seventeen baggies are one
    /// place where three jars and two baggies are two. And a stack that fits beside
    /// its own kind is no new place at all, which is why the room is asked about
    /// rather than assumed away.
    /// </para>
    /// </remarks>
    public static PortionSplit Split(
        int packagesInSlot,
        int unitsPerPackage,
        int order,
        IReadOnlyList<int> packagings,
        int stackLimit,
        IReadOnlyList<int>? roomBeside)
    {
        if (packagesInSlot <= 0 || order <= 0 || stackLimit <= 0 || packagings is null)
        {
            return default;
        }

        // Nothing to cut: the package does not hold more than the order, so the
        // ordinary fill uses it whole and this was never the right question.
        if (unitsPerPackage <= order)
        {
            return default;
        }

        // The portion is one stack and that is deliberate. The plan counts a cut
        // as one position on the handover screen, and a portion arriving as two
        // would make that count a guess — four positions is the game's limit and a
        // handover that overruns it is refused after the goods have left the bag.
        // So the portion has to go into a single packaging, and where it will not,
        // there is no cut.
        if (!OneStack(order, packagings, stackLimit, out int portionUnits, out int portions))
        {
            return default;
        }

        // The remainder is under no such bound: it goes into the bag, not onto the
        // screen. One stack wherever one stack will do, and beside the player's own
        // if there is room there — every stack is a place in his bag, and he has
        // nine of them with a bat and a skateboard in two.
        List<PortionStack>? leftover =
            Stacks(unitsPerPackage - order, packagings, stackLimit, roomBeside);

        if (leftover is null)
        {
            return default;
        }

        return new PortionSplit(
            portionUnits,
            portions,
            leftover,

            // One package in the slot and the slot empties itself as that package
            // leaves, so the remainder's first stack goes back where it came from
            // and only the others need room. More than one and it cannot: the
            // others stay, packaged differently, and a slot holds one kind of
            // thing.
            packagesInSlot > 1 ? leftover.Count : leftover.Count - 1);
    }

    /// <summary>
    /// How to package <paramref name="units"/> as one stack: the largest packaging
    /// that divides them exactly, if the packages will fit one slot.
    /// </summary>
    /// <remarks>
    /// Largest, because that is the fewest packages — both what keeps the count
    /// inside a slot and what the owner asked for, <i>"ich komme gar nicht mehr
    /// mit den Tüten hinterher"</i>. Exactly, never rounded: a part-filled package
    /// would either short-change the customer or invent product.
    /// </remarks>
    private static bool OneStack(
        int units,
        IReadOnlyList<int> packagings,
        int stackLimit,
        out int unitsPerPackage,
        out int packages)
    {
        unitsPerPackage = 0;
        packages = 0;

        for (int i = 0; i < packagings.Count; i++)
        {
            int size = packagings[i];

            if (size <= 0 || units % size != 0 || size <= unitsPerPackage)
            {
                continue;
            }

            if (units / size > stackLimit)
            {
                continue;
            }

            unitsPerPackage = size;
            packages = units / size;
        }

        return packages > 0;
    }

    /// <summary>
    /// How to package <paramref name="units"/> as stacks, or null when it cannot be
    /// done in <see cref="MostStacks"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Fewest stacks wins, and a stack that merges wins among those.</b> A stack
    /// is a place in the bag; a package is not. So seventeen units are seventeen
    /// baggies in one place rather than three jars and two baggies in two, and if
    /// the player already carries baggies of this product with room for seventeen,
    /// they cost him no place at all.
    /// </para>
    /// <para>
    /// Where no single packaging will hold the lot — the slot is smaller than the
    /// number of smallest packages, and no bigger packaging divides it — it falls
    /// back to filling biggest-first, which is how a person would do it and gives
    /// the fewest stacks of the ones left.
    /// </para>
    /// </remarks>
    private static List<PortionStack>? Stacks(
        int units,
        IReadOnlyList<int> packagings,
        int stackLimit,
        IReadOnlyList<int>? roomBeside)
    {
        if (units <= 0)
        {
            return null;
        }

        // One stack if one stack will do. Among the sizes that manage it: one that
        // fits beside the player's own stacks first, because that costs no place in
        // the bag at all, and then the SMALLEST.
        //
        // Smallest, which is the opposite of the portion going out, and the reason
        // is what happens next. The remainder is what the following contracts are
        // served from, and baggies serve an order of any size exactly while jars
        // only serve multiples of five — fifteen units back as three jars means the
        // next order of two has to open a jar and find somewhere for three baggies,
        // where fifteen baggies would simply have answered it. Both are one place in
        // the bag, so there is nothing to trade away.
        int best = 0;
        bool merges = false;

        for (int i = 0; i < packagings.Count; i++)
        {
            int size = packagings[i];

            if (size <= 0 || units % size != 0 || units / size > stackLimit)
            {
                continue;
            }

            bool fitsBeside = roomBeside is not null
                && i < roomBeside.Count
                && roomBeside[i] >= units / size;

            if (best == 0 || (fitsBeside && !merges) || (fitsBeside == merges && size < best))
            {
                best = size;
                merges = fitsBeside;
            }
        }

        if (best > 0)
        {
            return new List<PortionStack> { new(best, units / best) };
        }

        var stacks = new List<PortionStack>();

        while (units > 0)
        {
            if (stacks.Count == MostStacks)
            {
                return null;
            }

            int size = 0;

            for (int i = 0; i < packagings.Count; i++)
            {
                if (packagings[i] > size && packagings[i] > 0 && packagings[i] <= units)
                {
                    size = packagings[i];
                }
            }

            if (size == 0)
            {
                // Something is left over and nothing the product comes in is small
                // enough for it. No cut.
                return null;
            }

            int packages = units / size;
            if (packages > stackLimit)
            {
                packages = stackLimit;
            }

            stacks.Add(new PortionStack(size, packages));
            units -= packages * size;
        }

        return stacks.Count > 0 ? stacks : null;
    }
}

/// <summary>
/// One package opened: what crosses, what comes back, how each side is packaged,
/// and how much room putting the remainder away needs.
/// <see cref="Possible"/> is false for a cut that must not happen.
/// </summary>
public readonly struct PortionSplit
{
    private readonly IReadOnlyList<PortionStack> leftover;

    internal PortionSplit(
        int portionUnits,
        int portions,
        IReadOnlyList<PortionStack> leftover,
        int slotsNeeded)
    {
        PortionUnits = portionUnits;
        Portions = portions;
        this.leftover = leftover;
        SlotsNeeded = slotsNeeded;
    }

    /// <summary>Whether this package can be opened at all.</summary>
    public bool Possible => Portions > 0 && leftover is { Count: > 0 };

    /// <summary>
    /// Units in each package going to the customer — which packaging to put the
    /// portion in.
    /// </summary>
    public int PortionUnits { get; }

    /// <summary>
    /// Packages going to the customer. With any whole packages their units come
    /// to the order exactly, which is the point of cutting, and they are one stack
    /// and so one position on the handover screen.
    /// </summary>
    public int Portions { get; }

    /// <summary>
    /// What goes back into the bag, largest packaging first. One entry per stack,
    /// and a stack is a slot: three jars and two baggies are two places.
    /// </summary>
    public IReadOnlyList<PortionStack> Leftover =>
        leftover ?? System.Array.Empty<PortionStack>();

    /// <summary>
    /// How many <em>empty</em> inventory slots the remainder needs. One fewer than
    /// its stacks when the opened package was alone in its slot, because that slot
    /// empties itself as the package leaves.
    /// </summary>
    public int SlotsNeeded { get; }
}

/// <summary>One write to the inventory during a cut.</summary>
public readonly struct CutWrite
{
    internal CutWrite(int lot, int packages, int stack)
    {
        Lot = lot;
        Packages = packages;
        Stack = stack;
    }

    /// <summary>The lot whose slot gives up packages, or <c>-1</c>.</summary>
    public int Lot { get; }

    /// <summary>How many packages leave that slot.</summary>
    public int Packages { get; }

    /// <summary>
    /// The stack of the remainder this write puts in the bag, or <c>-1</c>. With a
    /// <see cref="Lot"/> as well, one write does both: the stack replaces all the
    /// slot held.
    /// </summary>
    public int Stack { get; }
}

/// <summary>What is out of the bag at one moment of a cut.</summary>
public readonly struct CutGoods
{
    internal CutGoods(IReadOnlyList<int> wholeLots, bool portion, IReadOnlyList<int> stacks)
    {
        WholeLots = wholeLots;
        Portion = portion;
        Stacks = stacks;
    }

    /// <summary>The lots whose whole packages are out.</summary>
    public IReadOnlyList<int> WholeLots { get; }

    /// <summary>Whether the portion is out.</summary>
    public bool Portion { get; }

    /// <summary>The stacks of the remainder that are not back in the bag.</summary>
    public IReadOnlyList<int> Stacks { get; }
}

/// <summary>One stack: this many packages of this size.</summary>
public readonly struct PortionStack
{
    internal PortionStack(int unitsPerPackage, int packages)
    {
        UnitsPerPackage = unitsPerPackage;
        Packages = packages;
    }

    /// <summary>Units in one package.</summary>
    public int UnitsPerPackage { get; }

    /// <summary>How many packages, never more than one slot will hold.</summary>
    public int Packages { get; }

    /// <summary>What the stack comes to.</summary>
    public int Units => UnitsPerPackage * Packages;
}
