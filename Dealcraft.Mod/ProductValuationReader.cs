using System;
using Dealcraft.Core;
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
/// be bisected at all; see <c>docs/native-truth.md</c>. It is the same probe the
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

            int quantity = OrderableQuantity(customer, product);
            if (quantity == 0)
            {
                // Not on this customer's list. The ordinary case, and silent:
                // most customers cannot order most products.
                continue;
            }

            PriceCurveRange range = PriceCurveRange.For(
                quantity,
                ProductManager.MIN_PRICE,
                ProductManager.MAX_PRICE,
                Customer.MaxOrderQuantityPerProduct,
                SearchPlanner.ProbeBudget);

            if (!range.Searchable)
            {
                // The one thing the pass could never say before. A quantity the
                // game's own limit forbids used to reach OfferBounds, which
                // refused it — rightly — and took the whole product's reading
                // with it, naming only the step size. Here it costs this
                // customer and says the figure.
                valuation.Notes.Add($"{CustomerName(customer)}: {range.Refusal}");
                continue;
            }

            probed++;
            valuation.Candidates.Add(new ProductCandidate(
                CustomerName(customer),
                quantity,
                HighestAcceptableTotal(customer, product, range, settings),
                appeal));
        }
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
                return entry.Item2;
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
