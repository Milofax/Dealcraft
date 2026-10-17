using System;

namespace Dealcraft.Core;

/// <summary>
/// The band of total prices one customer's order may be searched over, built
/// out of the game's own limits and nothing chosen here.
/// </summary>
/// <remarks>
/// <para>
/// This arithmetic used to sit inline in <c>ProductValuationReader</c>, where it
/// could not be tested without the game, and it is the arithmetic that broke:
/// the ceiling was <c>MAX_PRICE × quantity</c> against a one-dollar step, and on
/// four of the owner's sessions that product came out over
/// <see cref="int.MaxValue"/> and <c>OfferBounds</c> refused it — correctly, and
/// without being able to say which figure was absurd. It is here so that the
/// refusal happens where the figures can be named and where a test can drive it.
/// </para>
/// <para>
/// <b>Both ends of the band are the game's, and they are the same two numbers in
/// two places.</b> <c>ProductManager.MIN_PRICE</c> and
/// <c>ProductManager.MAX_PRICE</c> are metadata literals <c>1</c> and <c>999</c>;
/// <c>ProductManager.RpcLogic___SetPrice</c> clamps every
/// price that is ever written between the floats
/// (<c>1.0</c>, loaded) and
/// (<c>999.0</c>, loaded) and rounds to a whole dollar.
/// So a customer ceiling outside that band is a price the mod could not list
/// even if it found it, and searching for one costs probes for an answer that
/// would be clamped away.
/// </para>
/// <para>
/// <b>The order size is the game's too.</b>
/// <c>Customer.MaxOrderQuantityPerProduct</c> is a metadata literal
/// <c>1000</c> — the most units of one product the game will put in one order.
/// A quantity above it did not come from an order, and this is the only place
/// that can say so with the number in hand.
/// </para>
/// </remarks>
public readonly struct PriceCurveRange
{
    /// <summary>
    /// The step the search works in. A dollar is the smallest step the player
    /// can set a price in, and it is also what the game rounds a written price
    /// to (<c>RpcLogic___SetPrice</c>), so a finer grid
    /// would answer with prices nobody could ask for or see.
    /// </summary>
    public const float DollarStep = 1f;

    private readonly string? refusal;
    private readonly OfferBounds bounds;

    private PriceCurveRange(string? refusal, OfferBounds bounds)
    {
        this.refusal = refusal;
        this.bounds = bounds;
    }

    /// <summary>Whether there is a band to search at all.</summary>
    public bool Searchable => refusal is null;

    /// <summary>
    /// Why there is no band, naming the figure that was refused. Empty when
    /// there is one; always populated when there is not, because it goes in the
    /// log and in the debug record.
    /// </summary>
    public string Refusal => refusal ?? string.Empty;

    /// <summary>The bounds to hand <see cref="PriceCurveSearch"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// When there is no band. A caller that reads this without asking
    /// <see cref="Searchable"/> is asking for a price nobody named.
    /// </exception>
    public OfferBounds Bounds => Searchable
        ? bounds
        : throw new InvalidOperationException(Refusal);

    /// <summary>
    /// The band for one customer's order, or the sentence saying why there is
    /// none.
    /// </summary>
    /// <param name="orderQuantity">
    /// How many units the game says this customer would order.
    /// </param>
    /// <param name="lowestPricePerUnit">
    /// <c>ProductManager.MIN_PRICE</c>: the lowest price the game will hold.
    /// </param>
    /// <param name="highestPricePerUnit">
    /// <c>ProductManager.MAX_PRICE</c>: the highest price the game will hold.
    /// </param>
    /// <param name="largestOrder">
    /// <c>Customer.MaxOrderQuantityPerProduct</c>: the most units of one product
    /// the game will put in one order.
    /// </param>
    /// <param name="probeBudget">
    /// Probes the whole curve may cost — <see cref="SearchPlanner.ProbeBudget"/>.
    /// </param>
    public static PriceCurveRange For(
        int orderQuantity,
        int lowestPricePerUnit,
        int highestPricePerUnit,
        int largestOrder,
        int probeBudget)
    {
        // The game's own figures first: if these are not limits, nothing said
        // about the quantity underneath them would mean anything either.
        if (largestOrder < 1)
        {
            return No($"the game gives its largest order for one product as {largestOrder} units");
        }

        if (lowestPricePerUnit < 1)
        {
            return No($"the game gives its lowest listable price as {lowestPricePerUnit} a unit");
        }

        if (highestPricePerUnit < lowestPricePerUnit)
        {
            return No(
                $"the game gives a listable price band of {lowestPricePerUnit} to " +
                $"{highestPricePerUnit} a unit, which runs downwards");
        }

        if (orderQuantity < 1)
        {
            return No($"the game answered with an order of {orderQuantity} units");
        }

        if (orderQuantity > largestOrder)
        {
            return No(
                $"the game answered with an order of {orderQuantity} units of one product, " +
                $"and its own limit is {largestOrder}");
        }

        // Unreachable while the four figures above are the game's own — 999 a
        // unit across 1,000 units is 999,000 dollar steps. It is here because it
        // is the condition OfferBounds refuses on (OfferBounds.cs:83), and this
        // is where it can be refused with both factors in the sentence instead
        // of with the step size alone.
        if ((long)highestPricePerUnit * orderQuantity > int.MaxValue)
        {
            return No(
                $"an order of {orderQuantity} units at up to {highestPricePerUnit} a unit is " +
                "more dollar steps than the search can address");
        }

        return new PriceCurveRange(
            null,
            new OfferBounds(
                orderQuantity,
                orderQuantity,
                lowestPricePerUnit * (float)orderQuantity,
                highestPricePerUnit * (float)orderQuantity,
                DollarStep,
                probeBudget));
    }

    private static PriceCurveRange No(string refusal) => new(refusal, default);
}
