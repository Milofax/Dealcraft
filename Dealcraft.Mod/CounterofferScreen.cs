using System;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Messages;
using UnityEngine.UI;

namespace Dealcraft;

/// <summary>What the player has on the counteroffer screen right now.</summary>
internal readonly struct OpenCounteroffer
{
    public OpenCounteroffer(
        CounterofferInterface screen,
        Customer customer,
        ProductDefinition product,
        int quantity,
        float price,
        CounterofferLimits limits,
        string fairPrice)
    {
        Screen = screen;
        Customer = customer;
        Product = product;
        Quantity = quantity;
        Price = price;
        Limits = limits;
        FairPrice = fairPrice;
    }

    /// <summary>The screen itself, so its own limits can be read off it.</summary>
    public CounterofferInterface Screen { get; }

    public Customer Customer { get; }

    public ProductDefinition Product { get; }

    public int Quantity { get; }

    /// <summary>The price the screen is currently offering.</summary>
    public float Price { get; }

    /// <summary>
    /// What this screen will hold: the quantities it allows and the package
    /// totals its price selector accepts. Read through
    /// <see cref="CounterofferControls"/>, which is where the host-side
    /// automation reads the same screen's limits from, so an offer the host
    /// sends is one the player could have dialled in here.
    /// </summary>
    public CounterofferLimits Limits { get; }

    /// <summary>
    /// The game's own fair-price text, exactly as its label spells it. Shown
    /// beside our recommendation; never parsed, never rewritten.
    /// </summary>
    public string FairPrice { get; }

    public string ProductId => Product != null ? Product.ID ?? string.Empty : string.Empty;

    public string ProductName => Product != null ? Product.Name ?? string.Empty : string.Empty;

    /// <summary>
    /// What the price curve depends on: the customer, the product, the price
    /// range, and the quantities the search will actually cover.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The quantity range is the key's business and the quantity currently
    /// typed in is not — except that with the host's quantity raise switched
    /// off the range <em>is</em> that quantity, which is exactly the behaviour
    /// wanted. Then editing the quantity by hand invalidates the curve and the
    /// price is worked out again for the new size; with the raise on, the search
    /// probes every quantity itself, so typing a new one changes nothing it
    /// would find and recomputing would be hundreds of probes into the customer
    /// for an identical answer.
    /// </para>
    /// <para>
    /// A value tuple rather than a formatted string, because the settler is
    /// asked for this once a frame for as long as the screen is open and a key
    /// built to be compared and thrown away should not be an allocation. The
    /// product is spelled by pointer for the same reason: its id is an interop
    /// read, and identity is all a cache key needs.
    /// </para>
    /// </remarks>
    public (IntPtr Customer, IntPtr Product, int LowestQuantity, int HighestQuantity,
        float MinPrice, float MaxPrice) CurveKey(in QuantityRange search) =>
        (Customer.Pointer, Product.Pointer, search.Lowest, search.Highest,
            Limits.MinPrice, Limits.MaxPrice);

    /// <summary>
    /// What the cheap reading depends on: the offer the player has actually
    /// filled in. The game's fair price is left out — it is a function of the
    /// product and the quantity, both already here, and including a label's text
    /// in a cache key would recompute forever if the game ever animated it.
    /// </summary>
    public (IntPtr Customer, IntPtr Product, int Quantity, float Price)
        OfferKey => (Customer.Pointer, Product.Pointer, Quantity, Price);
}

internal enum CounterofferState
{
    /// <summary>No offer is on screen, or none is filled in yet. Routine.</summary>
    NotOnScreen,

    /// <summary>An offer is on screen and every part of it was read.</summary>
    Open,

    /// <summary>An offer is on screen but the mod could not make it out.</summary>
    Unreadable,
}

/// <summary>The outcome of looking at the counteroffer screen.</summary>
internal readonly struct CounterofferLookup
{
    private CounterofferLookup(CounterofferState state, OpenCounteroffer counteroffer, string problem)
    {
        State = state;
        Counteroffer = counteroffer;
        Problem = problem;
    }

    public CounterofferState State { get; }

    /// <summary>Only meaningful when <see cref="State"/> is <see cref="CounterofferState.Open"/>.</summary>
    public OpenCounteroffer Counteroffer { get; }

    /// <summary>Why the screen could not be read. Empty unless it could not be.</summary>
    public string Problem { get; }

    public static CounterofferLookup NotOnScreen() =>
        new(CounterofferState.NotOnScreen, default, string.Empty);

    public static CounterofferLookup Open(OpenCounteroffer counteroffer) =>
        new(CounterofferState.Open, counteroffer, string.Empty);

    public static CounterofferLookup Unreadable(string problem) =>
        new(CounterofferState.Unreadable, default, problem);
}

/// <summary>
/// Finds the customer behind the open counteroffer screen. Nothing here writes
/// to the game: it reads the phone's own UI state and the customer registry.
/// </summary>
internal static class CounterofferScreen
{
    /// <summary>Look at the counteroffer screen and report what is on it.</summary>
    public static CounterofferLookup Look()
    {
        try
        {
            if (!MessagesApp.InstanceExists)
            {
                return CounterofferLookup.NotOnScreen();
            }

            MessagesApp app = MessagesApp.Instance;
            if (app == null)
            {
                return CounterofferLookup.NotOnScreen();
            }

            CounterofferInterface screen = app.CounterofferInterface;
            if (screen == null || !screen.IsOpen)
            {
                return CounterofferLookup.NotOnScreen();
            }

            ProductDefinition product = screen.selectedProduct;
            if (product == null)
            {
                // The player has opened the screen but not chosen a product yet.
                return CounterofferLookup.NotOnScreen();
            }

            MSGConversation conversation = screen.conversation;
            if (conversation is null)
            {
                return CounterofferLookup.Unreadable("the screen belongs to no conversation");
            }

            Customer customer = FindCustomerBehind(conversation);
            if (customer == null)
            {
                return CounterofferLookup.Unreadable("no unlocked customer owns this conversation");
            }

            AmountSelector prices = screen.PriceSelector;
            if (prices == null)
            {
                return CounterofferLookup.Unreadable("the screen offers no price");
            }

            return CounterofferLookup.Open(
                new OpenCounteroffer(
                    screen,
                    customer,
                    product,
                    screen.quantity,
                    prices.SelectedAmount,
                    CounterofferControls.LimitsOf(screen),
                    FairPriceText(screen)));
        }
        catch (Exception error)
        {
            // A screen that closes, or a customer that despawns, between two of
            // the reads above lands here rather than in the game's update loop.
            return CounterofferLookup.Unreadable($"the screen went away mid-read ({error.Message})");
        }
    }

    /// <summary>
    /// The game's fair price as its own label spells it, so the overlay can put
    /// it beside ours without inventing a second formatting of the same number.
    /// </summary>
    private static string FairPriceText(CounterofferInterface screen)
    {
        Text label = screen.FairPriceLabel;
        return label != null ? label.text ?? string.Empty : string.Empty;
    }

    /// <summary>
    /// Match a conversation to its customer. No vanilla member points from a
    /// conversation back to a customer, so the customers are asked instead.
    /// </summary>
    private static Customer FindCustomerBehind(MSGConversation conversation)
    {
        Il2CppSystem.Collections.Generic.List<Customer> customers = Customer.UnlockedCustomers;
        if (customers is null)
        {
            return null;
        }

        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];
            if (customer == null)
            {
                continue;
            }

            NPC npc = customer.NPC;
            if (npc == null)
            {
                continue;
            }

            MSGConversation theirs = npc.MSGConversation;
            if (theirs is not null && theirs.Pointer == conversation.Pointer)
            {
                return customer;
            }
        }

        return null;
    }
}
