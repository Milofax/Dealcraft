using System.Globalization;

namespace Dealcraft.Core;

/// <summary>What maintaining one product's listed price came to.</summary>
public enum ListedPriceOutcome
{
    /// <summary>The switch is off, so nothing is written for any product.</summary>
    LeaveAlone,

    /// <summary>
    /// No price can be computed for this product, so it is left where the player
    /// put it. Named rather than skipped in silence.
    /// </summary>
    CannotCompute,

    /// <summary>The listed price is already the computed one.</summary>
    AlreadyThere,

    /// <summary>Write this price.</summary>
    Write,
}

/// <summary>
/// Whether to write one product's listed price, and the sentence that says what
/// changed.
/// </summary>
/// <remarks>
/// <para>
/// Pure. It knows the listed price, the computed price and the switch, and knows
/// nothing about <c>ProductManager</c>. What a write actually does to a running
/// game is in <see cref="ListedPriceDecision"/>'s caller and in
/// <c>docs/listed-price-truth.md</c>.
/// </para>
/// <para>
/// <b>Turning the switch off leaves prices where they are.</b> There is no
/// restore: putting a product back to what it was before Dealcraft touched it is
/// a second behaviour, it needs a store of its own, and the owner did not ask for
/// it. The app says so where the switch is.
/// </para>
/// </remarks>
public readonly struct ListedPriceDecision
{
    private ListedPriceDecision(ListedPriceOutcome outcome, float price, string reason)
    {
        Outcome = outcome;
        Price = price;
        Reason = reason;
    }

    public ListedPriceOutcome Outcome { get; }

    /// <summary>The price to write. Zero for every outcome but a write.</summary>
    public float Price { get; }

    /// <summary>
    /// What happened, in figures. A price write changes every future offer from
    /// every customer, including the ones a dealer handles, so it is never
    /// silent.
    /// </summary>
    public string Reason { get; }

    /// <param name="maintaining">The player's <c>MaintainListedPrices</c> answer.</param>
    /// <param name="productName">The product, as the game names it.</param>
    /// <param name="listedPrice">What the player lists it at now.</param>
    /// <param name="recommendation">
    /// What <see cref="PricingRecommendation.Best"/> made of the customers who
    /// could order it.
    /// </param>
    public static ListedPriceDecision For(
        bool maintaining,
        string productName,
        float listedPrice,
        in PricingRecommendation recommendation)
    {
        string product = string.IsNullOrWhiteSpace(productName) ? "the product" : productName;

        if (!maintaining)
        {
            return new ListedPriceDecision(
                ListedPriceOutcome.LeaveAlone, 0f, "you set your own prices");
        }

        if (!recommendation.Available || recommendation.PricePerUnit <= 0f)
        {
            return new ListedPriceDecision(
                ListedPriceOutcome.CannotCompute,
                0f,
                $"{product}: nobody you know could order it, so its price is left at "
                    + $"{Money(listedPrice)}");
        }

        // The game rounds a written price to a whole dollar, so a computed price
        // that is not one would be compared against a figure the game never
        // holds and rewritten every pass.
        float price = Whole(recommendation.PricePerUnit);

        if (price == Whole(listedPrice))
        {
            return new ListedPriceDecision(
                ListedPriceOutcome.AlreadyThere,
                0f,
                $"{product}: already at {Money(price)}");
        }

        return new ListedPriceDecision(
            ListedPriceOutcome.Write,
            price,
            $"{product}: {Money(listedPrice)} becomes {Money(price)}, which "
                + $"{recommendation.CustomersReached} of your customers still clear "
                + $"({Money(recommendation.WeeklyTakings)} a week)");
    }

    /// <summary>
    /// A price as the game stores it. <c>ProductManager.RpcLogic___SetPrice</c>
    /// clamps to the game's own bounds and rounds to a whole dollar before it
    /// writes, so anything finer is a figure the player could never see.
    /// </summary>
    private static float Whole(float price) => (float)System.Math.Round(price);

    private static string Money(float amount) =>
        "$" + amount.ToString("0.##", CultureInfo.InvariantCulture);
}
