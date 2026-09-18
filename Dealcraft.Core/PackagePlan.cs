using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// One kind of packaged product the player is carrying: how big each package is
/// and how many of them are in that slot.
/// </summary>
/// <remarks>
/// A slot, not a package. The game stacks identical packages, so one inventory
/// slot can hold five baggies; <see cref="Packages"/> is how many of them there
/// are and <see cref="UnitsPerPackage"/> is what one of them is worth.
/// </remarks>
public readonly struct PackageLot
{
    public PackageLot(int unitsPerPackage, int packages)
    {
        UnitsPerPackage = unitsPerPackage < 1 ? 1 : unitsPerPackage;
        Packages = packages < 0 ? 0 : packages;
    }

    /// <summary>Units one package of this kind holds. A baggie is 1, a brick 20.</summary>
    public int UnitsPerPackage { get; }

    /// <summary>How many of them are in the slot.</summary>
    public int Packages { get; }

    /// <summary>What the whole slot comes to.</summary>
    public int Units => UnitsPerPackage * Packages;
}

/// <summary>Why a delivery is not being made.</summary>
public enum PackageFillOutcome
{
    /// <summary>The order is covered.</summary>
    Covered,

    /// <summary>
    /// The bag does not hold enough of it. The owner's own case: <i>"der will
    /// 20, ich habe aber nur 18"</i>.
    /// </summary>
    NotEnoughCarried,

    /// <summary>
    /// The bag holds enough but no four packages of it do — a contract for
    /// twenty against twenty-five baggies. A player cannot put more than four
    /// positions on the handover screen either, so neither does the mod.
    /// </summary>
    NeedsMorePackages,
}

/// <summary>
/// How far up the quality ladder a delivery may go: the contract's own grade, or
/// the lowest one at or above it that the bag can actually cover the order with.
/// </summary>
/// <remarks>
/// <para>
/// The player's answer to <i>Exactly the grade that was ordered</i>, and the one
/// behaviour <c>app.md</c> adds rather than takes away. Walking upward spends the
/// owner's best product at the price a lower grade was ordered at — a jar of
/// Heavenly going out on a contract that asked for Poor — and nobody chose that.
/// <see cref="Exactly"/> is the default, because a grade nobody ordered is stock
/// a contract asking for it would have paid full price for.
/// </para>
/// <para>
/// It says nothing about how much leaves the bag. Packages are indivisible
/// (<see cref="PackageLot"/>), and an order of six against two jars of five can
/// only be met with ten — so a delivery is exact in its grade and may still
/// overshoot in units, whichever answer is set.
/// </para>
/// </remarks>
public enum GradeReach
{
    /// <summary>
    /// The grade on the contract and nothing above it. Nothing goes out where
    /// only a better grade would cover the order.
    /// </summary>
    Exactly,

    /// <summary>
    /// The lowest grade at or above the contract's whose packages reach the
    /// order — what shipped before the question was asked.
    /// </summary>
    MayUseAHigherGrade,
}

/// <summary>
/// Which packages to hand over: the least product that covers the order.
/// </summary>
/// <remarks>
/// <para>
/// <b>Smallest package first is not least overshoot, and shipping it as though
/// it were cost real product.</b> Taking small packages first piles them on
/// while the large one is still needed afterwards, and the small ones then
/// become pure giveaway. Measured against a bag of two baggies, one jar of five
/// and one brick of twenty, 22 of 25 orders gave away more than they had to; at
/// an order of eight it emptied the bag — 27 units against a contract of 20 —
/// because both baggies and the jar went in first and the brick was still
/// required. The answer at eight is the brick, alone.
/// </para>
/// <para>
/// So the combinations are enumerated rather than walked in an order somebody
/// hoped was right. A player carries at most nine slots and may fill at most
/// four handover positions, so there are a few hundred of them.
/// </para>
/// <para>
/// The order of preference, and it is the whole of the rule:
/// </para>
/// <list type="number">
/// <item>covers the order — never short, because a short delivery is refused
/// anyway;</item>
/// <item>fewest total units — the giveaway is what this exists to
/// minimise;</item>
/// <item>fewest packages, on a tie.</item>
/// </list>
/// <para>
/// <b>Overshoot is never a reason to refuse.</b> An order of six against two
/// jars of five can only be met with ten: one jar is short and nothing between
/// them exists. Losing the deal to save four units is much the larger loss, and
/// the player is standing there and would hand over the two jars themselves. The
/// contract row says what the packaging forced; nothing blocks it.
/// </para>
/// </remarks>
public static class PackagePlan
{
    /// <summary>
    /// Most packages one handover may use. <c>HandoverScreen.CustomerSlotCount</c>
    /// is four, and the mod does not do what a player could not do by hand.
    /// </summary>
    /// <remarks>
    /// <b>Four packages, as the ticket says, and it is tighter than four
    /// positions.</b> A handover position holds an item stack, so three baggies
    /// out of one slot fill one position rather than three — a player really can
    /// put four stacks on that screen. Where the two readings differ this costs
    /// units: three baggies, a jar and two bricks against an order of 28 can be
    /// met exactly out of five packages in three positions, and four packages
    /// cannot do better than two bricks, which is 40. Written to the ticket
    /// because the ticket is explicit; raising it to four <em>positions</em> is a
    /// change to this constant and to nothing else.
    /// </remarks>
    public const int MostPackages = 4;

    /// <summary>
    /// Choose the packages. The result is aligned with <paramref name="lots"/>:
    /// one count per lot, in the order they were given.
    /// </summary>
    public static PackageFill Fill(IReadOnlyList<PackageLot> lots, int order)
    {
        if (lots is null)
        {
            throw new ArgumentNullException(nameof(lots));
        }

        int carrying = 0;
        foreach (PackageLot lot in lots)
        {
            carrying += lot.Units;
        }

        if (order <= 0)
        {
            return PackageFill.Nothing(PackageFillOutcome.Covered, carrying, lots.Count);
        }

        var chosen = new int[lots.Count];
        var best = new int[lots.Count];
        int bestUnits = 0;
        int bestPackages = 0;
        bool found = false;

        void Search(int at, int units, int packages)
        {
            // Already worse than something that covers the order: every deeper
            // branch only adds units, so there is nothing below this worth
            // looking at.
            if (found && (units > bestUnits || (units == bestUnits && packages >= bestPackages)))
            {
                return;
            }

            if (units >= order)
            {
                found = true;
                bestUnits = units;
                bestPackages = packages;
                Array.Copy(chosen, best, chosen.Length);
                return;
            }

            if (at >= lots.Count || packages >= MostPackages)
            {
                return;
            }

            int most = lots[at].Packages;
            int room = MostPackages - packages;
            if (most > room)
            {
                most = room;
            }

            // Most of this kind first: a branch that reaches the order sooner
            // sets a bound the shallower branches are then pruned against.
            for (int take = most; take >= 0; take--)
            {
                chosen[at] = take;
                Search(at + 1, units + (take * lots[at].UnitsPerPackage), packages + take);
                chosen[at] = 0;
            }
        }

        Search(0, 0, 0);

        if (found)
        {
            return new PackageFill(PackageFillOutcome.Covered, best, bestUnits, bestPackages, carrying);
        }

        // Nothing is handed over either way; which of the two it is decides what
        // the contract row says, and the two are different problems.
        return PackageFill.Nothing(
            carrying < order ? PackageFillOutcome.NotEnoughCarried : PackageFillOutcome.NeedsMorePackages,
            carrying,
            lots.Count);
    }

    /// <summary>
    /// Which grade to hand over <em>and</em> which packages of it, as one
    /// answer: the lowest grade that meets the contract and whose packages can
    /// actually reach the order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The grade and the packaging are one decision.</b> The owner put it
    /// exactly — <i>"Qualitätsstufe, aber auch Menge zu vorhandenen
    /// Verpackungen"</i> — and deciding them in turn loses contracts. Five
    /// baggies of Standard against an order of five Poor has the units and
    /// cannot deliver them, because a handover holds
    /// <see cref="MostPackages"/> packages; a jar of Heavenly in the same bag
    /// covers it exactly. Choosing the grade first refuses the contract while
    /// the product to fill it is in the player's pocket.
    /// </para>
    /// <para>
    /// The order of preference is the one already shipped, with the packaging
    /// question added to it: the <em>lowest</em> grade that meets the contract,
    /// because every tier above it is stock a contract asking for that tier
    /// would have paid full price for, and then the least product of that grade
    /// that covers the order. It does not spend a better grade to save units:
    /// four baggies of Heavenly against an order of four is not an improvement
    /// on a jar of Standard, it is four units of Heavenly gone.
    /// </para>
    /// <para>
    /// <b>Whether it walks upward at all is the player's.</b>
    /// <see cref="GradeReach.Exactly"/> stops the walk at the contract's own
    /// grade, so a jar of Heavenly stays in the bag rather than going out on a
    /// contract that asked for Poor. Nothing else about the search changes, and
    /// nothing about package sizes changes either way.
    /// </para>
    /// <para>
    /// Where nothing covers the order, the grade reported is the adequate grade
    /// with the most of it in reach — the one a player would fix — and the fill
    /// carries which of the two problems it was.
    /// </para>
    /// </remarks>
    /// <param name="lots">
    /// Every package of one product in reach, each carrying its own grade.
    /// </param>
    /// <param name="requestedQuality">The grade on the contract.</param>
    /// <param name="order">Units the contract asks for.</param>
    /// <param name="reach">
    /// Whether the walk upward is allowed at all. Required rather than
    /// defaulted: the two answers deliver different goods, and a caller that did
    /// not say which it wanted is a caller that has not read the setting.
    /// </param>
    public static GradeFill Choose(
        IReadOnlyList<CarriedLot> lots, int requestedQuality, int order, GradeReach reach)
    {
        if (lots is null)
        {
            throw new ArgumentNullException(nameof(lots));
        }

        if (order <= 0)
        {
            return new GradeFill(
                covers: true,
                requestedQuality,
                PackageFill.Nothing(PackageFillOutcome.Covered, 0, lots.Count));
        }

        PackageFill nearest = PackageFill.Nothing(PackageFillOutcome.NotEnoughCarried, 0, lots.Count);
        int nearestQuality = requestedQuality;
        bool anyAdequateGrade = false;

        // Lowest first: the first grade that covers the order is the cheapest
        // one that does. Exactly the grade that was ordered is the same search
        // with the walk stopped where it starts — one bound, not a second
        // search.
        for (int quality = Lowest(requestedQuality); quality <= Highest(requestedQuality, reach); quality++)
        {
            PackageFill fill = OfGrade(lots, quality, order);
            if (fill.Carrying <= 0)
            {
                continue;
            }

            if (fill.Covers)
            {
                return new GradeFill(covers: true, quality, fill);
            }

            // The grade a player would go and fetch more of: the one there is
            // most of already.
            if (!anyAdequateGrade || fill.Carrying > nearest.Carrying)
            {
                nearest = fill;
                nearestQuality = quality;
                anyAdequateGrade = true;
            }
        }

        return new GradeFill(covers: false, nearestQuality, nearest);
    }

    /// <summary>
    /// Every way this order could be covered out of this bag, and no way that
    /// wastes a package it did not need.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Fill"/> answers for one contract standing alone. Two contracts
    /// wanting the same packages cannot both be answered that way — one of them
    /// has to take its second choice, or neither is covered — so the allocation
    /// needs the choices rather than the choice. See
    /// the contract spread.
    /// </para>
    /// <para>
    /// Only <b>minimal covers</b> are returned: a selection where taking any one
    /// package back out would leave the order short. Anything else spends
    /// product no contract asked for, and it can never help another contract
    /// either, because it holds packages that were free to give away.
    /// </para>
    /// <para>
    /// <b>One grade per delivery.</b> Splitting an order across two grades
    /// changes the mean quality difference the game pays the Exceeded Quality
    /// Bonus on into something nobody chose, so each option draws on one grade
    /// only — the same rule <see cref="GradeChoice"/> keeps.
    /// </para>
    /// <para>
    /// Bounded by the packaging: no option holds more than
    /// <see cref="MostPackages"/> packages, so there are at most a few thousand
    /// of them for any bag a player can carry, and they are enumerated in a
    /// fixed order so that an allocation built from them is the same on every
    /// run.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<PackageFill> Options(
        IReadOnlyList<CarriedLot> lots, int requestedQuality, int order, GradeReach reach)
    {
        if (lots is null)
        {
            throw new ArgumentNullException(nameof(lots));
        }

        var options = new List<PackageFill>();

        if (order <= 0)
        {
            options.Add(PackageFill.Nothing(PackageFillOutcome.Covered, 0, lots.Count));
            return options;
        }

        // The same bound as Choose, for the same reason: an allocation weighing
        // options must not be handed one that spends a grade the player said
        // never to spend.
        for (int quality = Lowest(requestedQuality); quality <= Highest(requestedQuality, reach); quality++)
        {
            MinimalCovers(lots, quality, order, options);
        }

        return options;
    }

    /// <summary>
    /// The minimal covers drawn from one grade, appended in a fixed order.
    /// </summary>
    private static void MinimalCovers(
        IReadOnlyList<CarriedLot> lots, int quality, int order, List<PackageFill> options)
    {
        var where = new List<int>();
        int carrying = 0;

        for (int i = 0; i < lots.Count; i++)
        {
            if (lots[i].Quality == quality && lots[i].Packages > 0)
            {
                where.Add(i);
                carrying += lots[i].Units;
            }
        }

        if (carrying < order)
        {
            return;
        }

        var chosen = new int[where.Count];

        void Search(int at, int units, int packages)
        {
            if (at >= where.Count || packages >= MostPackages)
            {
                return;
            }

            CarriedLot lot = lots[where[at]];
            int room = MostPackages - packages;
            int most = lot.Packages < room ? lot.Packages : room;

            for (int take = 0; take <= most; take++)
            {
                chosen[at] = take;
                int now = units + (take * lot.UnitsPerPackage);

                if (now < order)
                {
                    Search(at + 1, now, packages + take);
                    continue;
                }

                if (Minimal(lots, where, chosen, now, order))
                {
                    options.Add(Taken(lots, where, chosen, now, packages + take, carrying));
                }

                // Every larger take of this same lot covers the order too, and
                // one of its packages is then removable — so it is not minimal
                // and neither is anything below it.
                break;
            }

            chosen[at] = 0;
        }

        Search(0, 0, 0);
    }

    /// <summary>
    /// Whether every package in this selection is needed: taking one back out
    /// leaves the order short.
    /// </summary>
    private static bool Minimal(
        IReadOnlyList<CarriedLot> lots, List<int> where, int[] chosen, int units, int order)
    {
        for (int at = 0; at < chosen.Length; at++)
        {
            if (chosen[at] > 0 && units - lots[where[at]].UnitsPerPackage >= order)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// One grade's selection, said in the whole bag's index space so that every
    /// option an allocation weighs can be read against the same lots.
    /// </summary>
    private static PackageFill Taken(
        IReadOnlyList<CarriedLot> lots,
        List<int> where,
        int[] chosen,
        int units,
        int packages,
        int carrying)
    {
        var taken = new int[lots.Count];

        for (int at = 0; at < chosen.Length; at++)
        {
            taken[where[at]] = chosen[at];
        }

        return new PackageFill(PackageFillOutcome.Covered, taken, units, packages, carrying);
    }

    /// <summary>
    /// What <see cref="Fill"/> makes of one grade, said in the whole bag's index
    /// space. <see cref="PackageFill.Carrying"/> stays what it has always been:
    /// the units of <em>that</em> grade in reach, which is what the contract row
    /// means by "carrying 18 of 20".
    /// </summary>
    private static PackageFill OfGrade(IReadOnlyList<CarriedLot> lots, int quality, int order)
    {
        var mine = new List<PackageLot>();
        var where = new List<int>();

        for (int i = 0; i < lots.Count; i++)
        {
            if (lots[i].Quality == quality && lots[i].Packages > 0)
            {
                mine.Add(lots[i].Lot);
                where.Add(i);
            }
        }

        PackageFill fill = Fill(mine, order);
        var taken = new int[lots.Count];

        for (int at = 0; at < where.Count; at++)
        {
            taken[where[at]] = fill.From(at);
        }

        return new PackageFill(fill.Outcome, taken, fill.Units, fill.Packages, fill.Carrying);
    }

    /// <summary>
    /// The lowest grade worth weighing: the contract's own, held inside the
    /// ladder so a contract carrying a grade the game does not have is answered
    /// from the bottom rather than not at all.
    /// </summary>
    private static int Lowest(int requestedQuality) =>
        requestedQuality < QualityTier.Lowest ? QualityTier.Lowest : requestedQuality;

    /// <summary>
    /// The highest grade worth weighing: the top of the ladder, or the
    /// contract's own grade where the player asked for exactly what was ordered.
    /// This is the whole of the setting — one loop bound, and the search below
    /// it is unchanged.
    /// </summary>
    private static int Highest(int requestedQuality, GradeReach reach) =>
        reach == GradeReach.Exactly ? Lowest(requestedQuality) : QualityTier.Highest;
}

/// <summary>
/// One slot of packaged product, with the grade it carries. A bag is a list of
/// these: the grade and the package size travel together, which is the whole
/// reason <see cref="PackagePlan.Choose"/> exists.
/// </summary>
public readonly struct CarriedLot
{
    public CarriedLot(int quality, int unitsPerPackage, int packages)
    {
        Quality = quality;
        Lot = new PackageLot(unitsPerPackage, packages);
    }

    /// <summary>The grade, on <see cref="QualityTier"/>'s ladder.</summary>
    public int Quality { get; }

    /// <summary>The packages themselves.</summary>
    public PackageLot Lot { get; }

    /// <summary>Units one package of this kind holds.</summary>
    public int UnitsPerPackage => Lot.UnitsPerPackage;

    /// <summary>How many of them are in the slot.</summary>
    public int Packages => Lot.Packages;

    /// <summary>What the whole slot comes to.</summary>
    public int Units => Lot.Units;

    public override string ToString() =>
        $"{QualityTier.Name(Quality)} {Packages} x {UnitsPerPackage}";
}

/// <summary>
/// The answer to one contract line: which grade, and which packages of it.
/// </summary>
public readonly struct GradeFill
{
    internal GradeFill(bool covers, int quality, PackageFill fill)
    {
        Covers = covers;
        Quality = quality;
        Fill = fill;
    }

    /// <summary>Whether the order is covered at all.</summary>
    public bool Covers { get; }

    /// <summary>
    /// The grade handed over. Where nothing covers the order this is the
    /// adequate grade there is most of in reach, which is the one a player would
    /// go and fetch more of.
    /// </summary>
    public int Quality { get; }

    /// <summary>
    /// Which packages, in the index space of the bag it was chosen from, and
    /// which of the two problems it was where it does not cover.
    /// </summary>
    public PackageFill Fill { get; }

    /// <summary>What it comes to. Zero where nothing is handed over.</summary>
    public int Units => Fill.Units;

    /// <summary>How many packages that is.</summary>
    public int Packages => Fill.Packages;
}

/// <summary>What one handover would take out of the bag.</summary>
public readonly struct PackageFill
{
    private readonly int[] taken;

    internal PackageFill(PackageFillOutcome outcome, int[] taken, int units, int packages, int carrying)
    {
        Outcome = outcome;
        this.taken = taken;
        Units = units;
        Packages = packages;
        Carrying = carrying;
    }

    public PackageFillOutcome Outcome { get; }

    /// <summary>Whether anything is handed over at all.</summary>
    public bool Covers => Outcome == PackageFillOutcome.Covered;

    /// <summary>What it comes to. Zero when nothing is handed over.</summary>
    public int Units { get; }

    /// <summary>How many packages that is. Zero when nothing is handed over.</summary>
    public int Packages { get; }

    /// <summary>Everything of this product in the bag, whether used or not.</summary>
    public int Carrying { get; }

    /// <summary>
    /// How many packages to take from that lot, by the lot's position in the
    /// list handed to <see cref="PackagePlan.Fill"/>.
    /// </summary>
    public int From(int lot) => lot >= 0 && lot < taken.Length ? taken[lot] : 0;

    /// <summary>How many lots this answer covers.</summary>
    public int Lots => taken.Length;

    internal static PackageFill Nothing(PackageFillOutcome outcome, int carrying, int lots) =>
        new(outcome, new int[lots < 0 ? 0 : lots], 0, 0, carrying);
}
