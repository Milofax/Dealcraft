using System;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;

namespace Dealcraft;

/// <summary>
/// What a customer is offering right now, read off the game once so that the
/// gate, the curve and the RPC all work from one reading.
/// </summary>
internal readonly struct OfferedContract
{
    private OfferedContract(
        PendingOffer offer,
        ProductDefinition product,
        string productId,
        int quantity,
        float payment)
    {
        Offer = offer;
        Product = product;
        ProductId = productId;
        Quantity = quantity;
        Payment = payment;
    }

    /// <summary>The plain-value half, which is all the decision core sees.</summary>
    public PendingOffer Offer { get; }

    /// <summary>The product being asked for, or null when it could not be resolved.</summary>
    public ProductDefinition Product { get; }

    /// <summary>The product's id, as the counter-offer RPC wants it spelled.</summary>
    public string ProductId { get; }

    /// <summary>How many units the customer is asking for.</summary>
    public int Quantity { get; }

    /// <summary>What the customer is offering to pay for the whole package.</summary>
    public float Payment { get; }

    /// <summary>Whether there is enough here to price and counter.</summary>
    public bool IsPriceable => Product != null && Quantity > 0 && Offer.HasOfferedContract;

    /// <summary>
    /// Read one customer's standing offer. A customer that despawned mid-read
    /// comes back with nothing on the table rather than throwing into the
    /// game's update loop.
    /// </summary>
    /// <remarks>
    /// The first product entry and the contract's payment, which is exactly what
    /// <c>Customer.CounterOfferClicked</c> (RVA 0x6A7100) takes to open the
    /// screen with: <c>offeredContractInfo.Products.entries[0]</c> for the
    /// product id and the quantity, <c>offeredContractInfo.Payment</c> for the
    /// price. A counter-offer names one product, so the first entry is the
    /// offer as far as this screen is concerned — the game's choice, not ours.
    /// </remarks>
    public static OfferedContract Read(Customer customer)
    {
        try
        {
            NPC npc = customer.NPC;
            string name = npc == null ? string.Empty : npc.FullName ?? string.Empty;
            ContractInfo info = customer.OfferedContractInfo;

            var offer = new PendingOffer(
                OfferedContractKey.Of(customer),
                name,
                hasOfferedContract: info is not null,
                alreadyOnADeal: customer.CurrentContract != null,
                alreadyCountered: info is not null && info.IsCounterOffer);

            if (info is null)
            {
                return new OfferedContract(offer, null, string.Empty, 0, 0f);
            }

            ReadFirstProduct(info, out ProductDefinition product, out string productId, out int quantity);

            return new OfferedContract(offer, product, productId, quantity, info.Payment);
        }
        catch (Exception)
        {
            return new OfferedContract(
                new PendingOffer(string.Empty, string.Empty, false, false), null, string.Empty, 0, 0f);
        }
    }

    private static void ReadFirstProduct(
        ContractInfo info,
        out ProductDefinition product,
        out string productId,
        out int quantity)
    {
        product = null;
        productId = string.Empty;
        quantity = 0;

        ProductList products = info.Products;
        if (products is null || products.entries is null || products.entries.Count == 0)
        {
            return;
        }

        ProductList.Entry entry = products.entries[0];
        if (entry is null || string.IsNullOrWhiteSpace(entry.ProductID))
        {
            return;
        }

        productId = entry.ProductID;
        quantity = entry.Quantity;
        product = Il2CppScheduleOne.Registry.GetItem<ProductDefinition>(productId);
    }
}

/// <summary>
/// Reads the three values a standing offer is named by off the customer, and
/// hands them to <see cref="ContractKey"/>, which spells the key.
/// </summary>
/// <remarks>
/// <para>
/// One reader, used by every feature that claims an offered contract, so that
/// two of them keying the same offer cannot come to spell it differently. Each
/// feature still keeps a registry of its own: countering an offer and scheduling
/// it are two acts on one contract, and a shared registry would let the first
/// stand in for the second.
/// </para>
/// <para>
/// The spelling itself is in the core, where it can be tested — the day index it
/// carries has to survive midnight, and that is a rule about plain values rather
/// than about the game. This half is the interop call and nothing else.
/// </para>
/// </remarks>
internal static class OfferedContractKey
{
    /// <summary>
    /// The key for this customer's standing offer, or empty when there is none
    /// to key.
    /// </summary>
    public static string Of(Customer customer)
    {
        try
        {
            if (customer.OfferedContractInfo is null)
            {
                return string.Empty;
            }

            NPC npc = customer.NPC;
            GameDateTime offered = customer.OfferedContractTime;

            return ContractKey.ForOffer(
                npc == null ? null : npc.ID, offered.elapsedDays, offered.time);
        }
        catch (Exception)
        {
            // A customer that despawned mid-read cannot be keyed, and an offer
            // that cannot be keyed is one the registry refuses — which is the
            // safe end: nothing is acted on.
            return string.Empty;
        }
    }
}
