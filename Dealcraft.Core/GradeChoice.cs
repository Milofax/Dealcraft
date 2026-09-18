using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// The grade question. A contract fixes the product, the quantity and the
/// payment, and it fixes a quality too — but the goods that answer it carry a
/// real grade, and the difference between the two is money.
///
/// Three things in the game react to that difference, and they disagree, which
/// is why this is worth a type of its own. All three were read out of
/// GameAssembly.dll and are written up in <c>docs/handover-truth.md</c>; the
/// constants below are the game's, not estimates:
///
/// <list type="number">
/// <item><description>
/// <b>What the delivery scores.</b> <c>ProductItemInstance.GetSimilarity</c>
/// contributes <c>clamp01(delivered / requested) * 0.3</c>. Meeting the
/// contract's grade takes the whole term; <em>exceeding it adds nothing</em>.
/// </description></item>
/// <item><description>
/// <b>What the customer thinks of it.</b> <c>Customer.GetProductEnjoyment</c>
/// compares the grade against their <c>Standards</c>, not against the contract,
/// and the term saturates one tier above: Premium and Heavenly are worth the
/// same to a customer whose standard is Standard.
/// </description></item>
/// <item><description>
/// <b>The Exceeded Quality Bonus.</b> <c>Payment * 0.15 * qualityDifference</c>,
/// linear in tiers above the contract's grade and the only term without a
/// ceiling.
/// </description></item>
/// </list>
///
/// So handing over the contract's own grade is already the best delivery the
/// customer can score, and every tier above it is a straight trade: 15% of the
/// payment against stock that a contract asking for that grade would have taken
/// at full value. Dealcraft takes the cheapest grade that answers the contract
/// and tells the player what the others would have paid.
/// </summary>
public static class GradeChoice
{
    /// <summary>
    /// The fraction of the contract's payment one whole tier above the
    /// requested grade is worth.
    /// </summary>
    public const float QualityBonusRate = 0.15f;

    /// <summary>
    /// The game builds no bonus at all below this quality difference. A
    /// single-grade delivery only ever lands on whole tiers, so in practice
    /// this means "one tier or more", but the game's own number is kept rather
    /// than the simplification.
    /// </summary>
    public const float QualityBonusThreshold = 0.2f;

    /// <summary>
    /// Work out what to hand over. <paramref name="stock"/> is what is in
    /// reach, one entry per grade; grades outside the game's ladder are ignored
    /// rather than ranked on a guess.
    /// </summary>
    /// <remarks>
    /// Units only, and it always walks upward. Nothing in the mod calls it — the
    /// handover asks the overload below, which takes the packages and the
    /// player's answer to <i>exactly the grade that was ordered</i> — so this one
    /// is the older reading, kept for what it explains rather than for what it
    /// decides.
    /// </remarks>
    public static GradePlan Plan(DeliveryRequest request, IEnumerable<GradeStock> stock)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (stock is null)
        {
            throw new ArgumentNullException(nameof(stock));
        }

        List<GradeOutcome> outcomes = Rank(request, stock);

        if (outcomes.Count == 0)
        {
            return GradePlan.Nothing(
                request,
                $"the contract asks for {request.RequestedQuantity} of {request.ProductId} "
                    + "and there is none in reach",
                outcomes);
        }

        if (request.RequestedQuantity <= 0)
        {
            return GradePlan.Nothing(request, "the contract asks for no units", outcomes);
        }

        // Lowest first, so the first grade that both meets the contract's grade
        // and fills the order is the cheapest one that does.
        foreach (GradeOutcome outcome in outcomes)
        {
            if (outcome.Quality < request.RequestedQuality || !outcome.CoversTheContract)
            {
                continue;
            }

            return GradePlan.Deliver(
                request,
                outcome.Quality,
                request.RequestedQuantity,
                Chose(request, outcome),
                outcomes);
        }

        return GradePlan.Nothing(request, WhyNot(request, outcomes), outcomes);
    }

    /// <summary>
    /// Work out what to hand over when the packages are known as well as the
    /// units — which is the only way it can honestly be worked out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The overload above takes units per grade, and units are not what leaves
    /// the inventory: packages are. A grade can hold the units and be unable to
    /// deliver them, because a handover holds
    /// <see cref="PackagePlan.MostPositions"/> stacks — five baggies of
    /// Standard against an order of five is the case, and choosing the grade
    /// first refuses the contract while a jar of Heavenly that covers it sits in
    /// the same pocket. So the grade and the packaging are one question here,
    /// and <see cref="PackagePlan.Choose"/> answers it.
    /// </para>
    /// <para>
    /// Everything else is unchanged: the same ranking of what is in reach, the
    /// same lowest-adequate-grade rule, the same lines out of
    /// <see cref="Describe"/>. What the plan says is handed over is now
    /// something the bag can actually hand over.
    /// </para>
    /// </remarks>
    /// <param name="lots">
    /// Every package of this product in reach, each carrying its own grade.
    /// </param>
    /// <param name="reach">
    /// Whether a grade above the contract's may stand in for it. The player's
    /// answer, off the preferences file; <see cref="GradeReach.Exactly"/> is
    /// what a fresh install does.
    /// </param>
    /// <param name="fill">
    /// Which packages the chosen grade would take, in <paramref name="lots"/>'
    /// own index space. Where nothing covers the order it carries which of the
    /// two problems it was, for the contract row.
    /// </param>
    public static GradePlan Plan(
        DeliveryRequest request,
        IReadOnlyList<CarriedLot> lots,
        GradeReach reach,
        out PackageFill fill)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (lots is null)
        {
            throw new ArgumentNullException(nameof(lots));
        }

        List<GradeOutcome> outcomes = Rank(request, Stock(lots));
        GradeFill choice = PackagePlan.Choose(
            lots, request.RequestedQuality, request.RequestedQuantity, reach);
        fill = choice.Fill;

        if (outcomes.Count == 0)
        {
            return GradePlan.Nothing(
                request,
                $"the contract asks for {request.RequestedQuantity} of {request.ProductId} "
                    + "and there is none in reach",
                outcomes);
        }

        if (request.RequestedQuantity <= 0)
        {
            return GradePlan.Nothing(request, "the contract asks for no units", outcomes);
        }

        if (!choice.Covers)
        {
            return GradePlan.Nothing(request, WhyNotPacked(request, outcomes, choice, reach), outcomes);
        }

        return GradePlan.Deliver(
            request,
            choice.Quality,
            request.RequestedQuantity,
            ChosePacked(request, choice),
            outcomes);
    }

    /// <summary>
    /// What handing over one grade would come to, whether or not there is
    /// enough of it.
    /// </summary>
    /// <param name="quality">The grade being weighed.</param>
    /// <param name="requestedQuality">The grade on the contract.</param>
    /// <param name="contractPayment"><c>Contract.Payment</c>.</param>
    /// <param name="customerStandard">
    /// The customer's standards on the quality ladder. See
    /// <see cref="DeliveryRequest.CustomerStandard"/>.
    /// </param>
    /// <param name="unitsAvailable">Units of this grade in reach.</param>
    /// <param name="requestedQuantity">Units the contract asks for.</param>
    public static GradeOutcome Score(
        int quality,
        int requestedQuality,
        float contractPayment,
        int customerStandard,
        int unitsAvailable,
        int requestedQuantity)
    {
        float difference = quality - requestedQuality;
        float bonus = difference >= QualityBonusThreshold
            ? contractPayment * QualityBonusRate * difference
            : 0f;

        int matched = unitsAvailable < requestedQuantity ? unitsAvailable : requestedQuantity;
        if (matched < 0)
        {
            matched = 0;
        }

        return new GradeOutcome(
            quality,
            unitsAvailable,
            matched,
            coversTheContract: requestedQuantity > 0 && unitsAvailable >= requestedQuantity,
            qualityDifference: difference,
            qualityBonus: bonus,
            clearsStandards: quality >= customerStandard);
    }

    /// <summary>
    /// The plan in the words a player would use. One line for what is being
    /// handed over, one for every other grade in reach, and one for the grades
    /// that fall short of what this customer expects.
    /// </summary>
    /// <param name="formatMoney">
    /// How the game spells amounts, so the log and the game agree.
    /// </param>
    public static IReadOnlyList<string> Describe(GradePlan plan, Func<float, string> formatMoney)
    {
        if (plan is null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (formatMoney is null)
        {
            throw new ArgumentNullException(nameof(formatMoney));
        }

        var lines = new List<string>();
        DeliveryRequest request = plan.Request;

        lines.Add(plan.CanDeliver
            ? $"Handing over {plan.ChosenUnits} x {QualityTier.Name(plan.ChosenQuality)} {request.ProductId}. "
                + plan.Reason
            : $"Not handing over {request.ProductId}: {plan.Reason}.");

        foreach (GradeOutcome outcome in plan.Outcomes)
        {
            if (plan.CanDeliver && outcome.Quality == plan.ChosenQuality)
            {
                continue;
            }

            lines.Add(AlsoInReach(request, outcome, formatMoney));
        }

        string standards = Standards(plan);
        if (standards.Length > 0)
        {
            lines.Add(standards);
        }

        return lines;
    }

    private static List<GradeOutcome> Rank(DeliveryRequest request, IEnumerable<GradeStock> stock)
    {
        var outcomes = new List<GradeOutcome>();

        foreach (GradeStock entry in stock)
        {
            if (!QualityTier.IsKnown(entry.Quality) || entry.Units <= 0)
            {
                continue;
            }

            outcomes.Add(Score(
                entry.Quality,
                request.RequestedQuality,
                request.ContractPayment,
                request.CustomerStandard,
                entry.Units,
                request.RequestedQuantity));
        }

        outcomes.Sort((left, right) => left.Quality.CompareTo(right.Quality));
        return outcomes;
    }

    /// <summary>
    /// The units of each grade in reach, summed out of the packages. The
    /// ranking, and every line <see cref="Describe"/> writes, is about units, so
    /// this is where the packages stop mattering and the grades start.
    /// </summary>
    private static List<GradeStock> Stock(IReadOnlyList<CarriedLot> lots)
    {
        var units = new int[QualityTier.Highest + 1];

        foreach (CarriedLot lot in lots)
        {
            if (QualityTier.IsKnown(lot.Quality))
            {
                units[lot.Quality] += lot.Units;
            }
        }

        var stock = new List<GradeStock>();

        for (int quality = QualityTier.Lowest; quality <= QualityTier.Highest; quality++)
        {
            if (units[quality] > 0)
            {
                stock.Add(new GradeStock(quality, units[quality]));
            }
        }

        return stock;
    }

    /// <summary>
    /// Why this grade, when the packages were asked as well. Two sentences: the
    /// contract's own grade, or the lowest one above it the packages can
    /// actually reach.
    /// </summary>
    private static string ChosePacked(DeliveryRequest request, GradeFill choice)
    {
        string grade = QualityTier.Name(choice.Quality);

        if (choice.Quality == request.RequestedQuality)
        {
            return $"{grade} is what the contract asks for, so the delivery matches it exactly "
                + "and no stock is spent on a grade the customer did not order.";
        }

        return $"{grade} is the lowest grade in reach that meets the contract's "
            + $"{QualityTier.Name(request.RequestedQuality)} and whose packages reach "
            + $"{request.RequestedQuantity} units.";
    }

    /// <summary>
    /// Why nothing goes out, when the packages were asked as well. The third
    /// case is the one the units-only reading could not see: the product is
    /// there, and it is in too many packages to put on the screen.
    /// </summary>
    /// <remarks>
    /// The fourth is the setting's own, and it is the whole reason the setting
    /// has to say something: exactly the grade that was ordered refuses a
    /// delivery a better grade would have covered, and the player cannot see
    /// from inside the game that the better grade is why nothing happened. So
    /// the sentence names the setting rather than describing a shortage that is
    /// not there.
    /// </remarks>
    private static string WhyNotPacked(
        DeliveryRequest request,
        IReadOnlyList<GradeOutcome> outcomes,
        GradeFill choice,
        GradeReach reach)
    {
        if (reach == GradeReach.Exactly)
        {
            string held = HeldBack(request, outcomes);
            if (held.Length > 0)
            {
                return held;
            }
        }

        if (choice.Fill.Outcome != PackageFillOutcome.NeedsMorePackages)
        {
            return WhyNot(request, outcomes);
        }

        return $"{QualityTier.Name(choice.Quality)} has {choice.Fill.Carrying} units in reach and no "
            + $"{PackagePlan.MostPositions} stacks of it come to the "
            + $"{request.RequestedQuantity} the contract asks for — a handover holds "
            + $"{PackagePlan.MostPositions} stacks, so this needs fewer, larger packages";
    }

    /// <summary>
    /// The sentence for a delivery the setting refused: a better grade in reach
    /// has the units and is not being spent. Empty where the setting is not what
    /// stopped it, so that the ordinary shortages keep their own words.
    /// </summary>
    private static string HeldBack(DeliveryRequest request, IReadOnlyList<GradeOutcome> outcomes)
    {
        // Sorted lowest first, so the first one above the contract's grade that
        // has the units is the one the walk upward would have reached.
        foreach (GradeOutcome outcome in outcomes)
        {
            if (outcome.Quality <= request.RequestedQuality || !outcome.CoversTheContract)
            {
                continue;
            }

            return $"the contract asks for {QualityTier.Name(request.RequestedQuality)} and what is in "
                + $"reach of that grade does not cover the {request.RequestedQuantity} units; "
                + $"{QualityTier.Name(outcome.Quality)} has them, and the handover is set to exactly "
                + "the grade that was ordered — so it stays in the bag";
        }

        return string.Empty;
    }

    private static string Chose(DeliveryRequest request, GradeOutcome outcome)
    {
        string grade = QualityTier.Name(outcome.Quality);

        if (outcome.Quality == request.RequestedQuality)
        {
            return $"{grade} is what the contract asks for, so the delivery matches it exactly "
                + "and no stock is spent on a grade the customer did not order.";
        }

        return $"{grade} is the lowest grade in reach that meets the contract's "
            + $"{QualityTier.Name(request.RequestedQuality)}.";
    }

    private static string WhyNot(DeliveryRequest request, IReadOnlyList<GradeOutcome> outcomes)
    {
        string requested = QualityTier.Name(request.RequestedQuality);
        int bestAtOrAboveGrade = 0;
        bool anyAtOrAboveGrade = false;

        foreach (GradeOutcome outcome in outcomes)
        {
            if (outcome.Quality >= request.RequestedQuality)
            {
                anyAtOrAboveGrade = true;
                if (outcome.UnitsAvailable > bestAtOrAboveGrade)
                {
                    bestAtOrAboveGrade = outcome.UnitsAvailable;
                }
            }
        }

        if (!anyAtOrAboveGrade)
        {
            return $"the contract asks for {requested} and nothing in reach is that good; "
                + "handing over a lower grade is a decision for a player, not for the automation";
        }

        // Splitting the order across two grades would change the quality
        // difference, and with it the bonus, into something nobody chose.
        return $"no single grade covers the {request.RequestedQuantity} units the contract asks for — "
            + $"the most of one {requested}-or-better grade in reach is {bestAtOrAboveGrade}";
    }

    private static string AlsoInReach(
        DeliveryRequest request,
        GradeOutcome outcome,
        Func<float, string> formatMoney)
    {
        string grade = QualityTier.Name(outcome.Quality);
        string head = $"Also in reach: {grade}, {outcome.UnitsAvailable} units";

        if (outcome.Quality < request.RequestedQuality)
        {
            return $"{head} — below the contract's {QualityTier.Name(request.RequestedQuality)}, "
                + "so it would cost satisfaction rather than earn anything.";
        }

        if (!outcome.CoversTheContract)
        {
            return $"{head} — not enough to fill the order on its own.";
        }

        if (!outcome.EarnsQualityBonus)
        {
            return $"{head} — matches the contract, no quality bonus either way.";
        }

        int tiers = outcome.Quality - request.RequestedQuality;
        return $"{head} — would add {formatMoney(outcome.QualityBonus)} "
            + $"({tiers} {(tiers == 1 ? "tier" : "tiers")} above the contract), "
            + "and spend stock a contract asking for it would have paid full price for.";
    }

    private static string Standards(GradePlan plan)
    {
        var below = new List<string>();
        var clears = new List<string>();

        foreach (GradeOutcome outcome in plan.Outcomes)
        {
            (outcome.ClearsStandards ? clears : below).Add(QualityTier.Name(outcome.Quality));
        }

        if (below.Count == 0)
        {
            return string.Empty;
        }

        string standard = QualityTier.Name(plan.Request.CustomerStandard);
        string line = $"{string.Join(", ", below)} "
            + $"{(below.Count == 1 ? "is" : "are")} below their standards ({standard})";

        return clears.Count == 0
            ? line + ", and nothing in reach clears them."
            : line + $"; {string.Join(", ", clears)} {(clears.Count == 1 ? "clears" : "clear")} them.";
    }
}
