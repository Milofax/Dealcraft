using System;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Product.Packaging;

namespace Dealcraft;

/// <summary>
/// Asks one customer, repeatedly, how likely they are to take an offer.
/// </summary>
/// <remarks>
/// <para>
/// <c>Customer.GetOfferSuccessChance</c> is the deterministic half of the game's
/// valuation: reading its native code shows nineteen calls, all getters and
/// scoring helpers, and no <c>Random</c>. Its sibling
/// <c>Customer.EvaluateCounteroffer</c> folds a <c>Random.Range</c> straight into
/// its returned bool, so the same offer can be refused and then accepted. Only
/// the chance may be searched over; a bisection across a rolled predicate
/// converges on noise rather than on a boundary.
/// </para>
/// <para>
/// The game scores actual items, so an offer has to be spelled out as the
/// product instance it would be handed over as. Building that instance is the
/// expensive part, and the curve asks about one quantity many times in a row
/// while it bisects the price, so the last one built is kept.
/// </para>
/// </remarks>
internal sealed class OfferChanceProbe
{
    private readonly Customer _customer;
    private readonly ProductDefinition _product;

    private Il2CppSystem.Collections.Generic.List<ItemInstance> _items;
    private int _builtFor = -1;

    public OfferChanceProbe(Customer customer, ProductDefinition product)
    {
        _customer = customer;
        _product = product;
    }

    /// <summary>
    /// Ask once, for a caller that is not searching: no item cache to keep and
    /// no reason to swallow a failure, because one bad reading is the whole
    /// answer rather than one probe out of a hundred. The offer is spelled for
    /// the game the same way either way, which is the point of it living here.
    /// </summary>
    public static float Ask(Customer customer, ProductDefinition product, int quantity, float totalPrice) =>
        customer.GetOfferSuccessChance(ItemsFor(product, quantity), totalPrice);

    /// <summary>
    /// What this probe has been handed and what it has been told, for the record
    /// to write out once the search is over.
    /// </summary>
    /// <remarks>
    /// A search asks this probe a few dozen times and everything below answers
    /// into here rather than logging, which is the difference between a
    /// diagnosis and a flood. Nothing reads it while the search runs.
    /// </remarks>
    public ProbeDiagnosis Diagnosis { get; } = new();

    /// <summary>
    /// The game's own chance that this customer takes <paramref name="quantity"/>
    /// units for <paramref name="totalPrice"/>, 0..1. A customer or product that
    /// has gone scores zero rather than throwing into the game's update loop.
    /// </summary>
    /// <remarks>
    /// The zero a failure answers with is the zero a refusal answers with, and
    /// that is deliberate: the search must not be given a special value to
    /// reason about, and a probe that failed is not evidence the customer would
    /// have said yes. What changed is that the failure is now written down
    /// instead of disappearing — four negotiations in the owner's session found
    /// a best chance of zero at every price, and from the file there was no way
    /// to tell which zero that was.
    /// </remarks>
    public float Chance(int quantity, float totalPrice)
    {
        try
        {
            if (_customer == null || _product == null)
            {
                // Not an exception and not spelled as one: nothing threw, the
                // thing being asked about had simply gone.
                Diagnosis.Failed(null, "the customer or the product had gone before the probe ran");
                return 0f;
            }

            float chance = _customer.GetOfferSuccessChance(ItemsFor(quantity), totalPrice);
            Diagnosis.Answered(quantity, totalPrice, chance);
            return chance;
        }
        catch (Exception error)
        {
            Diagnosis.Failed(error.GetType().Name, Said(error));
            return 0f;
        }
    }

    private Il2CppSystem.Collections.Generic.List<ItemInstance> ItemsFor(int quantity)
    {
        if (_items is not null && _builtFor == quantity)
        {
            return _items;
        }

        _items = ItemsFor(_product, quantity);
        _builtFor = quantity;

        // Read off the list that is about to be handed over, not off the
        // definition it was built from: what the customer scores is the
        // instance, and whether it carries a quality and a packaging at all is
        // the question the record is being asked to settle.
        Diagnosis.Offered(Describe(_product, quantity, _items));

        return _items;
    }

    /// <summary>
    /// What is in the item list, as plain values. The two absences it is looking
    /// for — no instance at all, and an instance with no packaging applied — are
    /// both answers rather than accidents, so neither is skipped over.
    /// </summary>
    private static ProbeOffering Describe(
        ProductDefinition product,
        int quantity,
        Il2CppSystem.Collections.Generic.List<ItemInstance> items)
    {
        string name = product.Name ?? product.ID;

        if (items.Count == 0)
        {
            return ProbeOffering.Nothing(name, quantity);
        }

        ProductItemInstance instance = items[0].TryCast<ProductItemInstance>();
        if (instance is null)
        {
            return ProbeOffering.Unreadable(name, quantity);
        }

        PackagingDefinition packaging = instance.AppliedPackaging;

        return ProbeOffering.Of(
            name,
            quantity,
            instance.Amount,
            (int)instance.Quality,
            packaging == null ? null : packaging.Name ?? packaging.ID);
    }

    /// <summary>
    /// An offer as the game wants to be handed it: the product instance it would
    /// actually be handed over as. The one place that knows this, so a reading
    /// and a probe cannot come to describe the same offer differently.
    /// </summary>
    private static Il2CppSystem.Collections.Generic.List<ItemInstance> ItemsFor(
        ProductDefinition product,
        int quantity)
    {
        var items = new Il2CppSystem.Collections.Generic.List<ItemInstance>();
        ItemInstance instance = product.GetDefaultInstance(quantity);
        if (instance is not null)
        {
            items.Add(instance);
        }

        return items;
    }

    /// <summary>
    /// What an exception said, with the frame that threw it.
    /// </summary>
    /// <remarks>
    /// The message alone is usually not enough. An interop failure says
    /// <c>Object reference not set to an instance of an object</c> and nothing
    /// about which call it was, and that is the only thing worth knowing. The
    /// innermost frame is the answer and the rest of the stack is noise in a
    /// record meant to be read by a person.
    /// </remarks>
    private static string Said(Exception trouble)
    {
        string where = string.Empty;

        try
        {
            Exception innermost = trouble;
            while (innermost.InnerException is not null)
            {
                innermost = innermost.InnerException;
            }

            string stack = innermost.StackTrace;
            if (!string.IsNullOrWhiteSpace(stack))
            {
                int end = stack.IndexOf('\n');
                where = " at " + (end < 0 ? stack : stack.Substring(0, end)).Trim();
            }

            if (!ReferenceEquals(innermost, trouble))
            {
                return trouble.Message
                    + " (inner " + innermost.GetType().Name + ": " + innermost.Message + ")" + where;
            }
        }
        catch (Exception)
        {
            // A diagnosis that throws while describing a throw helps nobody.
        }

        return trouble.Message + where;
    }

}
