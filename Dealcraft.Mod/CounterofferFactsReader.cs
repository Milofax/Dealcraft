using System;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Relation;
using Il2CppScheduleOne.Product;

namespace Dealcraft;

/// <summary>
/// Reads the seven figures <see cref="CounterofferOdds"/> works from.
/// </summary>
/// <remarks>
/// Everything here comes off the game and nothing is computed: the arithmetic
/// is in <c>Dealcraft.Core</c>, where a test can drive it without a game. This
/// is the whole of what the adapter owes the negotiation now, and it is less
/// than the probe it replaces — no item instance has to be built to ask.
/// </remarks>
internal static class CounterofferFactsReader
{
    /// <summary>
    /// What this customer's answer to a counter-offer will be built from, or
    /// nothing readable when the game cannot be asked.
    /// </summary>
    public static bool TryRead(
        Customer customer,
        ProductDefinition product,
        in OfferedContract offered,
        out CounterofferFacts facts,
        out string trouble)
    {
        facts = default;
        trouble = string.Empty;

        try
        {
            if (customer == null || product == null || !offered.IsPriceable)
            {
                trouble = "the customer, the product or the offer had gone";
                return false;
            }

            float enjoyment = customer.GetProductEnjoyment(product);
            float addiction = customer.CurrentAddiction;
            float relationship = Relationship(customer);

            facts = new CounterofferFacts(
                product.MarketValue,
                offered.Quantity,
                offered.Payment,
                enjoyment,
                addiction,
                relationship,
                BudgetPerOrder(customer, addiction, relationship));

            return true;
        }
        catch (Exception error)
        {
            trouble = $"{error.GetType().Name}: {error.Message}";
            return false;
        }
    }

    /// <summary>
    /// How much of one order's budget the customer has, which is what three
    /// times over is the hard ceiling on a counter-offer.
    /// </summary>
    /// <remarks>
    /// <b>The day list is ours and not the customer's.</b> The game's own
    /// <c>GetOrderDays</c> empties whatever list it is handed before it fills
    /// it, so passing the customer's cached one would clear the game's own
    /// state as a side effect of a reading. Zero when the game answers with no
    /// days at all, which reads as "no ceiling" and leaves the gate to the other
    /// terms rather than refusing everything.
    /// </remarks>
    private static float BudgetPerOrder(Customer customer, float addiction, float relationship)
    {
        CustomerData data = customer.CustomerData;
        if (data == null)
        {
            return 0f;
        }

        var days = new Il2CppSystem.Collections.Generic.List<EDay>();
        data.GetOrderDays(addiction, relationship, days);

        return days.Count > 0 ? data.GetAdjustedWeeklySpend(relationship) / days.Count : 0f;
    }

    /// <summary>
    /// The customer's normalised relationship, which is what the game's own
    /// spend and counter-offer arithmetic takes — not their addiction, although
    /// the two sit next to each other and this project once had them swapped.
    /// </summary>
    private static float Relationship(Customer customer)
    {
        NPC npc = customer.NPC;
        if (npc == null)
        {
            return 0f;
        }

        NPCRelationData relation = npc.RelationData;

        return relation == null ? 0f : relation.NormalizedRelationDelta;
    }
}
