using System;
using System.Globalization;
using Dealcraft.Core;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Messages;

namespace Dealcraft;

/// <summary>
/// The counteroffer screen's two controls: what they will accept, and how an
/// offer is dialled into them.
/// </summary>
/// <remarks>
/// <para>
/// One place. There were two callers once — the advisor overlay's Apply button
/// drove the controls directly — and the overlay was deleted with ticket 27.
/// The host-side automation is what is left, and it never opens a screen at all,
/// but still has to know what the screen would have allowed: an offer the player
/// could not have dialled in by hand is not an offer this mod sends.
/// </para>
/// <para>
/// The limits come off the live screen object whether or not it is open — they
/// are configured on the prefab, and <c>Open</c> reads <c>MaxQuantity</c>
/// rather than writing it (RVA 0x9DDC80). So the automation and the player are
/// reading the very same four numbers.
/// </para>
/// <para>
/// Nothing here decides anything. What to offer is
/// <see cref="CounterofferPlan"/>'s answer; this only asks the game and works
/// its controls.
/// </para>
/// </remarks>
internal static class CounterofferControls
{
    /// <summary>
    /// What the counteroffer screen will accept, or the reason it could not be
    /// asked.
    /// </summary>
    /// <remarks>
    /// The price range is the selector's own where it has one, and the screen's
    /// configured constants where it has not — the same precedence
    /// <see cref="CounterofferScreen"/> uses for an open screen, so the two
    /// readings cannot disagree about the same screen.
    /// </remarks>
    public static bool TryReadLimits(out CounterofferLimits limits, out string problem)
    {
        limits = default;

        try
        {
            if (!MessagesApp.InstanceExists)
            {
                problem = "the messages app is not in this scene";
                return false;
            }

            MessagesApp app = MessagesApp.Instance;
            CounterofferInterface screen = app == null ? null : app.CounterofferInterface;
            if (screen == null)
            {
                problem = "the messages app has no counteroffer screen";
                return false;
            }

            limits = LimitsOf(screen);
            problem = limits.Viable ? string.Empty : limits.Problem;
            return limits.Viable;
        }
        catch (Exception error)
        {
            // A scene coming down between two of the reads above lands here
            // rather than in the game's update loop.
            problem = $"the counteroffer screen could not be read ({error.Message})";
            return false;
        }
    }

    /// <summary>
    /// The four limits of one counteroffer screen, open or not.
    /// </summary>
    public static CounterofferLimits LimitsOf(CounterofferInterface screen)
    {
        AmountSelector prices = screen.PriceSelector;

        // The selector's own limits are what the player can actually dial in,
        // and therefore what an offer may name. The screen's configured bounds
        // stand in only where the selector has none.
        bool selectorBounded = prices != null && prices.MaxValue > prices.MinValue;

        return new CounterofferLimits(
            // Only the ceiling on quantity belongs to the instance: the other
            // three limits are fixed for every counteroffer in this build, and
            // the compiler enforces that reading.
            CounterofferInterface.MinQuantity,
            screen.MaxQuantity,
            selectorBounded ? prices.MinValue : CounterofferInterface.MinPrice,
            selectorBounded ? prices.MaxValue : CounterofferInterface.MaxPrice);
    }

    /// <summary>
    /// Put an offer on the screen, through the two entry points the player's
    /// own keyboard and mouse use. Answers what it actually managed to set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Quantity as a step, not as a destination.
    /// <c>CounterofferInterface.ChangeQuantity(float)</c> (RVA 0x9DD810) is the
    /// plus/minus button handler: it <em>adds</em> its argument to the quantity
    /// and then clamps. Handing it the wanted quantity outright would double
    /// the deal on the first press and double it again on the second, which is
    /// exactly the compounding the ticket forbids. Handing it the difference
    /// leaves the screen holding the plan, and a second press asks it for a
    /// step of zero.
    /// </para>
    /// <para>
    /// Its sibling <c>ChangeQuantity(string)</c> is the input field's own
    /// handler and does not write the field back. That matters, because
    /// <c>Send()</c> (RVA 0x9DDFD0) re-parses the field rather than reading the
    /// quantity — so setting the quantity through the string overload would
    /// send whatever the player last typed. The float overload writes the field
    /// with <c>SetTextWithoutNotify</c>, which is why it is the one used here.
    /// </para>
    /// </remarks>
    public static bool Apply(CounterofferInterface screen, in PlannedOffer offer, Action<string> warn)
    {
        if (!offer.HasOffer)
        {
            return false;
        }

        try
        {
            if (screen == null || !screen.IsOpen)
            {
                return false;
            }

            screen.ChangeQuantity(offer.QuantityStepFrom(screen.quantity));

            AmountSelector prices = screen.PriceSelector;
            if (prices == null)
            {
                warn("the counteroffer screen offers no price, so only the quantity was set");
                return false;
            }

            prices.SetAmount(offer.TotalPrice);
            return true;
        }
        catch (Exception error)
        {
            warn($"the recommendation could not be applied to the screen: {error.Message}");
            return false;
        }
    }

    /// <summary>
    /// What the screen is holding now, for a log line that reports the result
    /// rather than the intention.
    /// </summary>
    public static string Describe(CounterofferInterface screen)
    {
        try
        {
            if (screen == null)
            {
                return "nothing";
            }

            AmountSelector prices = screen.PriceSelector;
            float total = prices == null ? 0f : prices.SelectedAmount;
            return $"{screen.quantity.ToString(CultureInfo.InvariantCulture)} at {GameMoney.Rounded(total)}";
        }
        catch (Exception)
        {
            return "nothing";
        }
    }
}
