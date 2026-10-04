using System;
using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// The four hard limits the counteroffer screen puts on an offer: the
/// quantities it will hold and the package totals its price selector will
/// accept.
/// </summary>
/// <remarks>
/// These belong to the screen, not to the host's preferences, and they are the
/// same object's limits whether a player has the screen open or the automation
/// is working without one — so an offer the automation sends is one the player
/// could have dialled in by hand. A recommendation outside them is not a
/// recommendation.
/// </remarks>
public readonly struct CounterofferLimits
{
    private readonly string? problem;

    public CounterofferLimits(int minQuantity, int maxQuantity, float minPrice, float maxPrice)
    {
        MinQuantity = minQuantity;
        MaxQuantity = maxQuantity;
        MinPrice = minPrice;
        MaxPrice = maxPrice;
        problem = Fault(minQuantity, maxQuantity, minPrice, maxPrice);
    }

    /// <summary>The smallest quantity the screen will hold.</summary>
    public int MinQuantity { get; }

    /// <summary>The largest quantity the screen will hold.</summary>
    public int MaxQuantity { get; }

    /// <summary>The lowest package total the price selector will accept.</summary>
    public float MinPrice { get; }

    /// <summary>The highest package total the price selector will accept.</summary>
    public float MaxPrice { get; }

    /// <summary>
    /// The lowest quantity that may actually be offered: the screen's own
    /// figure, never below the floor the game hardcodes.
    /// </summary>
    public int LowestQuantity => Math.Max(ScreenLimits.SmallestQuantity, MinQuantity);

    /// <summary>
    /// Whether there is any offer at all inside these limits. False for limits
    /// that were read off a screen mid-teardown, or off one that has not been
    /// configured yet.
    /// </summary>
    public bool Viable => problem is null;

    /// <summary>
    /// Why there is no offer inside these limits. Empty when there is one;
    /// always populated when there is not, because it goes in the log.
    /// </summary>
    public string Problem => problem ?? string.Empty;

    public override string ToString() =>
        Viable
            ? $"{LowestQuantity}-{MaxQuantity} units at {Money(MinPrice)}-{Money(MaxPrice)}"
            : $"no offer is possible ({Problem})";

    private static string? Fault(int minQuantity, int maxQuantity, float minPrice, float maxPrice)
    {
        int lowest = Math.Max(ScreenLimits.SmallestQuantity, minQuantity);

        if (maxQuantity < lowest)
        {
            return $"the screen holds no quantity between {lowest} and {maxQuantity}";
        }

        if (float.IsNaN(minPrice) || float.IsNaN(maxPrice))
        {
            return "the screen did not report a price range";
        }

        if (minPrice < 0f)
        {
            return $"the screen's lowest price is {Money(minPrice)}, which is not a price";
        }

        if (maxPrice < minPrice)
        {
            return $"the screen accepts no total between {Money(minPrice)} and {Money(maxPrice)}";
        }

        return null;
    }

    private static string Money(float amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
