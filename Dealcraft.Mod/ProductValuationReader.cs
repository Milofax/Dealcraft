using System;
using System.Runtime.InteropServices;
using Dealcraft.Core;
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;

namespace Dealcraft;

/// <summary>
/// Reads what a product is worth to the customers who can order it. Every
/// figure comes from the game: who may order it and how much of it from
/// <c>Customer.GetOrderableProductsWithQuantities</c>, what they would pay from
/// the same bisection the advisor runs, and what they make of it from
/// <c>Customer.GetProductEnjoyment</c>. Nothing is written back.
/// </summary>
/// <remarks>
/// The bisection probes with <see cref="OfferChanceProbe"/> — the game's
/// <c>GetOfferSuccessChance</c> against the host's acceptance threshold — and
/// never with <c>Customer.EvaluateCounteroffer</c>, which folds a
/// <c>UnityEngine.Random.Range</c> roll into the bool it returns and so cannot
/// be bisected at all. It is the same probe the
/// counteroffer search runs, so the ceiling found here and the price the mod
/// would actually offer agree by construction rather than by coincidence.
/// <para>
/// This used to say "the same threshold the advisor overlay uses" and "the
/// ceiling this panel shows". The overlay and the panel are both deleted, and
/// the threshold's double life is <c>issues/44-the-floor-also-prices-the-shop.md</c>.
/// </para>
/// </remarks>
internal static class ProductValuationReader
{
    /// <summary>
    /// How many customers a single reading will probe. A reading happens when a
    /// product is selected, and each customer costs a bounded handful of probes;
    /// this keeps the worst case bounded as the player's book grows. Customers
    /// beyond it still count toward overall appeal, which costs one call each.
    /// </summary>
    private const int MaxCustomersProbed = 32;

    /// <summary>
    /// Ask the game what this product is worth, customers and all. A product
    /// read while the world is still coming up yields an unavailable reading,
    /// never a half-invented one.
    /// </summary>
    /// <param name="settings">
    /// The host's own, so that the ceiling found here is the price at the
    /// confidence they asked for and costs no more price points than they allow.
    /// </param>
    /// <remarks>
    /// There used to be a second entry point, <c>Summarise</c>, which read the
    /// properties and asked no customer — the cheap reading a master-list row
    /// was built from. The master list was deleted with the Products tab and
    /// nothing called it again, so it is gone and the customers are no longer
    /// optional.
    /// </remarks>
    public static ProductValuation Read(ProductDefinition product, AdvisorSettings settings)
    {
        // Taken before anything else and kept outside the try, so that a reading
        // the game breaks off still says which product it was about. It used to
        // not, and four of the owner's sessions recorded the same anonymous
        // failure as a result.
        string productId = string.Empty;
        string productName = string.Empty;

        try
        {
            if (product == null)
            {
                return ProductValuation.Unavailable("the product is gone");
            }

            productId = product.ID ?? string.Empty;
            productName = product.Name ?? productId;

            var valuation = new ProductValuation
            {
                Available = true,
                ProductId = productId,
                ProductName = productName,
                BasePrice = product.BasePrice,
                MarketValue = product.MarketValue,
                AskingPrice = AskingPrice(product),
            };

            ReadCustomers(product, valuation, settings);

            return valuation;
        }
        catch (Exception error)
        {
            // Reading a destroyed Il2Cpp object throws rather than returning
            // null, so the whole reading is abandoned instead of half-filled.
            return ProductValuation.Unavailable(
                $"the game broke off the reading ({error.Message})", productId, productName);
        }
    }

    /// <summary>
    /// What the player lists the product at. <c>GetPrice</c> is an instance
    /// method, so before a save is loaded there is no price to read and the
    /// definition's own market value stands in.
    /// </summary>
    /// <remarks>
    /// Shared with <see cref="CounterofferSearch"/>, which needs the same figure
    /// for the same reason: a player who has answered "use my listed price" is
    /// asking for this one. One reader, so the price the product panel reports
    /// and the floor the advisor holds to cannot be different numbers.
    /// </remarks>
    public static float AskingPrice(ProductDefinition product)
    {
        if (!NetworkSingleton<ProductManager>.InstanceExists)
        {
            return product.MarketValue;
        }

        ProductManager manager = NetworkSingleton<ProductManager>.Instance;
        return manager != null ? manager.GetPrice(product) : product.MarketValue;
    }

    /// <summary>
    /// Everyone the player knows: what they make of the product, and for those
    /// who could actually order it, how much and at what ceiling.
    /// </summary>
    private static void ReadCustomers(
        ProductDefinition product,
        ProductValuation valuation,
        AdvisorSettings settings)
    {
        Il2CppSystem.Collections.Generic.List<Customer> customers = Customer.UnlockedCustomers;
        if (customers is null)
        {
            return;
        }

        int probed = 0;

        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];
            if (customer == null)
            {
                continue;
            }

            float appeal = customer.GetProductEnjoyment(product);
            valuation.OverallAppeals.Add(appeal);

            if (probed >= MaxCustomersProbed)
            {
                continue;
            }

            probed++;
            valuation.Candidates.Add(new ProductCandidate(
                CustomerName(customer),
                CliffFor(appeal, product.MarketValue),
                appeal));
        }
    }

    /// <summary>
    /// The highest price at which this customer would still order this product
    /// at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Customer.GetWeightedRandomProduct</c> scores every
    /// product a customer could order with
    /// <c>appeal = enjoyment + 1 - clamp(listedPrice / MarketValue, 0, 2)</c>,
    /// and <c>Customer.TryGenerateContract</c> abandons the
    /// contract when the winner's appeal is under
    /// <c>Customer.MIN_ORDER_APPEAL</c>. Rearranged, the customer goes quiet
    /// above <c>(enjoyment + 1 - MIN_ORDER_APPEAL) x MarketValue</c>.
    /// </para>
    /// <para>
    /// <b>No order size appears in it, and that is the finding.</b> This used to
    /// bisect the game's acceptance chance over a price range built from
    /// <c>GetOrderableProductsWithQuantities</c>, which answers
    /// <c>int.MaxValue</c> for a customer with no dealer — honestly, meaning "no
    /// dealer limits this order". The listed price never needed that figure:
    /// what a customer pays is <c>scalar x price x quantity</c> while the
    /// quantity is <c>scalar x budget / price</c>, so the price cancels out of
    /// the money altogether and decides only whether they order.
    /// </para>
    /// <para>
    /// The clamp at 2 in the game's own expression is why this cannot answer
    /// above <c>2 x MarketValue</c>: past that the appeal stops falling, so a
    /// customer who is still silent there is silent for want of enjoyment
    /// rather than for the price, and no price brings them back.
    /// </para>
    /// </remarks>
    private static float CliffFor(float enjoyment, float marketValue)
    {
        if (marketValue <= 0f)
        {
            return 0f;
        }

        float ratio = enjoyment + 1f - Customer.MIN_ORDER_APPEAL;

        // The game clamps the ratio it subtracts, so nothing above twice the
        // market value is reachable and nothing below zero is meaningful.
        if (ratio > 2f)
        {
            ratio = 2f;
        }

        return ratio <= 0f ? 0f : ratio * marketValue;
    }

    /// <summary>
    /// How many units of this product the game says this customer would order,
    /// or zero when it is not on their list at all.
    /// </summary>
    /// <remarks>
    /// Shared with the stock reading once, which asked the same question for
    /// the other direction. <c>StockDemandReader</c> is deleted, so this is the
    /// only caller now; it is still one reader because the reason it was one —
    /// two counts of the same customers cannot be allowed to differ — did not
    /// depend on there being two.
    /// </remarks>
    /// <summary>
    /// The <c>int</c> in a <c>Tuple&lt;ProductDefinition, int&gt;</c>, read off
    /// the object's own runtime class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Neither of the two obvious ways works, and both fail quietly.</b>
    /// <c>entry.Item2</c> calls the game's generic <c>get_Item2()</c> and
    /// answered <c>2147483647</c> for every customer on every product;
    /// <c>entry.m_Item2</c> reads the field through the generated
    /// <c>NativeFieldInfoPtr_m_Item2</c> and answered <c>807774160</c>, which is
    /// — the low half of a heap address, so it was reading the
    /// <em>first</em> item, the product reference, as an integer. Both figures
    /// were identical across the whole roster, which is what gives them away:
    /// the game's own cap is 1000 units per product, printed by
    /// <c>ListedPricePass</c> in the same run.
    /// </para>
    /// <para>
    /// Il2CppInterop generates one wrapper class for <c>Tuple`2</c> and resolves
    /// its field pointers once, so they belong to whichever instantiation was
    /// resolved first and are wrong for the rest. The object itself is not
    /// ambiguous: its class knows where its own fields are. So the offset is
    /// taken from <c>il2cpp_object_get_class</c> of this very instance.
    /// </para>
    /// <para>
    /// Zero on any failure, which reads as "not on this customer's list" — the
    /// same answer a customer who cannot order the product gives, and the
    /// conservative one: it costs a reading rather than inventing an order.
    /// </para>
    /// </remarks>
    private static int SecondItem(Il2CppSystem.Tuple<ProductDefinition, int> entry)
    {
        try
        {
            IntPtr instance = entry.Pointer;
            if (instance == IntPtr.Zero)
            {
                return 0;
            }

            IntPtr type = IL2CPP.il2cpp_object_get_class(instance);
            if (type == IntPtr.Zero)
            {
                return 0;
            }

            IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(type, "m_Item2");
            if (field == IntPtr.Zero)
            {
                return 0;
            }

            // The offset is from the start of the object, header included, which
            // is what the pointer already points at.
            return Marshal.ReadInt32(instance, (int)IL2CPP.il2cpp_field_get_offset(field));
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static int OrderableQuantity(Customer customer, ProductDefinition product)
    {
        Il2CppSystem.Collections.Generic.List<Il2CppSystem.Tuple<ProductDefinition, int>> orderable =
            customer.GetOrderableProductsWithQuantities(customer.AssignedDealer);

        if (orderable is null)
        {
            return 0;
        }

        for (int i = 0; i < orderable.Count; i++)
        {
            Il2CppSystem.Tuple<ProductDefinition, int> entry = orderable[i];
            if (entry is null || entry.Item1 == null)
            {
                continue;
            }

            if (entry.Item1.Pointer == product.Pointer)
            {
                return SecondItem(entry);
            }
        }

        return 0;
    }

    /// <summary>
    /// The most this customer would pay for that many, found by the same
    /// bisection and the same probe the advisor uses: the game's success chance
    /// against the host's acceptance threshold. That predicate is deterministic
    /// and monotone in price, so the boundary is exact rather than estimated,
    /// and the returned price was observed accepted rather than merely inferred.
    /// </summary>
    /// <remarks>
    /// The band comes from <see cref="PriceCurveRange"/>, which is where the
    /// game's own price and order limits are read against each other. Nothing is
    /// computed here that a test could not reach.
    /// </remarks>
    private static float HighestAcceptableTotal(
        Customer customer,
        ProductDefinition product,
        in PriceCurveRange range,
        AdvisorSettings settings)
    {
        var probe = new OfferChanceProbe(customer, product);

        PriceCurve curve = PriceCurveSearch.Build(
            range.Bounds,
            OfferAcceptance.Probe(probe.Chance, settings.AcceptanceProbabilityThreshold));

        return curve.Points.Count > 0 && curve.Points[0].Accepted ? curve.Points[0].TotalPrice : 0f;
    }

    private static string CustomerName(Customer customer)
    {
        Il2CppScheduleOne.NPCs.NPC npc = customer.NPC;
        return npc != null ? npc.FullName ?? string.Empty : string.Empty;
    }
}
