using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// One customer who could order a product, and what they would pay for it. The
/// ceiling is the boundary <see cref="PriceCurveSearch"/> found by asking the
/// game, not a guess; the quantity is the size the game says they would order.
/// </summary>
public readonly struct ProductCandidate
{
    public ProductCandidate(string customerName, int orderQuantity, float highestAcceptableTotal, float appeal)
    {
        CustomerName = customerName;
        OrderQuantity = orderQuantity;
        HighestAcceptableTotal = highestAcceptableTotal;
        Appeal = appeal;
    }

    /// <summary>
    /// One customer and the highest price at which they would still order this
    /// product at all — their appeal cliff.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Customer.GetWeightedRandomProduct</c> scores each
    /// product with <c>appeal = enjoyment + 1 - clamp(listedPrice / MarketValue,
    /// 0, 2)</c>, and <c>Customer.TryGenerateContract</c> builds
    /// no contract at all when the winner's appeal is under
    /// <c>Customer.MIN_ORDER_APPEAL</c>. So the price at which this customer
    /// goes quiet is <c>(enjoyment + 1 - MIN_ORDER_APPEAL) x MarketValue</c>,
    /// and there is no order size in it.
    /// </para>
    /// <para>
    /// The quantity is one because there is nothing for it to do any more, and
    /// that is the point: the figure this used to carry was the one the game
    /// answered with <c>int.MaxValue</c>, and pricing never needed it.
    /// </para>
    /// </remarks>
    public ProductCandidate(string customerName, float cliffPerUnit, float appeal)
        : this(customerName, 1, cliffPerUnit, appeal)
    {
    }

    public string CustomerName { get; }

    /// <summary>
    /// How many units the game says this customer would order. One, and unused,
    /// for a candidate built from an appeal cliff.
    /// </summary>
    public int OrderQuantity { get; }

    /// <summary>The most they would pay for that many.</summary>
    public float HighestAcceptableTotal { get; }

    /// <summary>
    /// The game's own enjoyment score for this product and this customer. Plain
    /// decimal and may be negative.
    /// </summary>
    public float Appeal { get; }

    /// <summary>The figure a price is actually set in.</summary>
    public float PricePerUnit => OrderQuantity > 0 ? HighestAcceptableTotal / OrderQuantity : 0f;
}

/// <summary>
/// One price and what it would do: how many of the customers who could order
/// the product still clear it, and what it takes across them in a week.
/// </summary>
/// <remarks>
/// A rung of the ladder. One recommended number is advice; three price points
/// with the customer count beside them show the cliff — where raising the price
/// starts costing customers faster than it gains margin.
///
/// <b>Nothing draws them.</b> The Products tab did, and it is deleted; the
/// points go to the debug record, which is where the cliff can still be read
/// after the fact. See <c>app.md</c> on what deleting that tab costs.
/// </remarks>
public readonly struct ListedPricePoint
{
    public ListedPricePoint(float pricePerUnit, int customersReached, float weeklyTakings)
    {
        PricePerUnit = pricePerUnit;
        CustomersReached = customersReached;
        WeeklyTakings = weeklyTakings;
    }

    public float PricePerUnit { get; }

    /// <summary>How many candidates still tolerate this price.</summary>
    public int CustomersReached { get; }

    /// <summary>What it takes across them, per week.</summary>
    public float WeeklyTakings { get; }
}

/// <summary>
/// The one price to list a product at.
/// <para>
/// A single asking price has to serve every customer at once: raise it and the
/// ones who tolerate less stop buying, lower it and the ones who would have paid
/// more are subsidised. The best price is therefore the one maximising
/// <c>price x (units ordered by everyone who still tolerates it)</c>, and the
/// maximum always sits on some customer's own ceiling — between two ceilings
/// nothing changes but the price, so it pays to be at the higher one. Only those
/// ceilings are evaluated, and each is the exact boundary the ticket-03 search
/// found by asking the game.
/// </para>
/// <para>
/// <b>This is the listed price</b>, and the project notes says
/// what that number does: <c>ProductDefinition.Price</c> tail-jumps into
/// <c>ProductManager.GetPrice</c>, so the "base price" and the price the player
/// types into the Products app are one number, and
/// <c>Customer.TryGenerateContract</c> reads it while assembling the payment.
/// The list therefore sets the offers that arrive. It does not touch acceptance
/// at all — neither <c>GetOfferSuccessChance</c> nor
/// <c>EvaluateCounteroffer</c> reads it.
/// </para>
/// </summary>
public readonly struct PricingRecommendation
{
    private static readonly ListedPricePoint[] NoPoints = Array.Empty<ListedPricePoint>();

    private PricingRecommendation(
        bool available,
        float pricePerUnit,
        float weeklyTakings,
        int customersReached,
        IReadOnlyList<ListedPricePoint> points)
    {
        Available = available;
        PricePerUnit = pricePerUnit;
        WeeklyTakings = weeklyTakings;
        CustomersReached = customersReached;
        Points = points ?? NoPoints;
    }

    /// <summary>False when no customer could order the product at all.</summary>
    public bool Available { get; }

    /// <summary>What to ask per unit.</summary>
    public float PricePerUnit { get; }

    /// <summary>What that price takes across everyone who still buys at it.</summary>
    public float WeeklyTakings { get; }

    /// <summary>How many customers still buy at that price.</summary>
    public int CustomersReached { get; }

    /// <summary>
    /// Every price this call evaluated, cheapest first — one per distinct
    /// customer ceiling. These were always being worked out; they were simply
    /// thrown away once the winner was known.
    /// </summary>
    public IReadOnlyList<ListedPricePoint> Points { get; }

    public static PricingRecommendation Best(IReadOnlyList<ProductCandidate> candidates) =>
        Best(candidates, 0f);

    /// <summary>
    /// The price to list at, never below <paramref name="marketValue"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The floor exists because keeping every customer is not worth any
    /// price.</b> Without it one customer who barely likes the product sets the
    /// price for all of them, and every new customer of that kind sets it lower
    /// again — the owner saw where that ends: <i>"sonst optimiert es sich immer
    /// weiter, bis dann doch ein Kunde ein Ding für 5 Dollar haben möchte. Das
    /// geht natürlich nicht."</i>
    /// </para>
    /// <para>
    /// The market value is the floor rather than a number somebody picked,
    /// because below it there is nothing left to buy. The game's own acceptance
    /// terms are measured as a ratio against it and saturate at or under it, so
    /// a cheaper list does not make a customer likelier to order — it only
    /// hands them more units for the same money. A customer whose cliff sits
    /// under the market value is a customer this product cannot be sold to at a
    /// fair price, and they are allowed to go quiet.
    /// </para>
    /// </remarks>
    public static PricingRecommendation Best(
        IReadOnlyList<ProductCandidate> candidates,
        float marketValue)
    {
        var points = new List<ListedPricePoint>(candidates.Count);
        var seen = new List<float>(candidates.Count);

        float bestPrice = 0f;
        float bestTakings = 0f;
        int bestReach = 0;
        bool found = false;

        foreach (ProductCandidate asking in candidates)
        {
            float price = asking.PricePerUnit;

            // A cliff under the floor is raised to it: this customer is then
            // one of the ones priced out, and the point still records how many
            // that costs.
            if (price < marketValue)
            {
                price = marketValue;
            }

            if (price <= 0f)
            {
                // A customer who would pay nothing cannot set a price; they are
                // still counted among those reached by any price above zero.
                continue;
            }

            // Two customers with the same ceiling are one rung. A ladder that
            // listed the same price twice would read as two.
            if (seen.Contains(price))
            {
                continue;
            }

            seen.Add(price);

            ListedPricePoint point = At(price, candidates);
            points.Add(point);

            // Most customers kept wins, and ties go to the higher price.
            //
            // Both halves come out of the same reading. What a customer pays for
            // an order is scalar x listedPrice x quantity while the quantity is
            // scalar x budget / listedPrice, so the price cancels and the
            // payment is theirs, not yours to set — raising the list does not
            // raise the money, it only sends less stock out for the same cash.
            // What the price does decide is whether they order at all. So the
            // money follows the number of customers still ordering, and among
            // prices that keep the same customers the highest is free.
            if (point.CustomersReached > bestReach
                || (point.CustomersReached == bestReach && price > bestPrice))
            {
                bestPrice = price;
                bestTakings = point.WeeklyTakings;
                bestReach = point.CustomersReached;
                found = true;
            }
        }

        points.Sort((left, right) => left.PricePerUnit.CompareTo(right.PricePerUnit));

        return found && bestReach > 0
            ? new PricingRecommendation(true, bestPrice, bestTakings, bestReach, points)
            : new PricingRecommendation(false, 0f, 0f, 0, NoPoints);
    }

    /// <summary>
    /// What any price would do, not only a customer's ceiling. The price the
    /// player has actually listed is rarely one of the ceilings, and the panel
    /// has to say what it takes a week.
    /// </summary>
    public static ListedPricePoint At(float pricePerUnit, IReadOnlyList<ProductCandidate> candidates)
    {
        float takings = 0f;
        int reached = 0;

        foreach (ProductCandidate buyer in candidates)
        {
            if (buyer.PricePerUnit < pricePerUnit)
            {
                continue;
            }

            takings += pricePerUnit * buyer.OrderQuantity;
            reached++;
        }

        return new ListedPricePoint(pricePerUnit, reached, takings);
    }
}
