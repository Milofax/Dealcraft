using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Law;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Relation;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Handover;

namespace Dealcraft;

/// <summary>
/// Writes down what a handover actually paid.
/// </summary>
/// <remarks>
/// <para>
/// The owner is sure the Generosity Bonus paid him far more than ten dollars a
/// unit. The reading says <c>(delivered - requested) x 10</c>. Rather than
/// argue, this records what the game shows and puts our prediction beside it,
/// labelled as ours — so one evening of play settles it either way. That is why
/// nothing here computes a payment: the arithmetic is the thing under test, so
/// it must not be what produces the measurement.
/// </para>
/// <para>
/// Two observations make a row, and the row says which ones it got. See
/// <see cref="HandoverLedgerPatches"/> for why those two seams and not the ones
/// the spec first named.
/// </para>
/// <para>
/// Nothing here may throw: every entry point is called from inside a Harmony
/// patch on the game's own handover path. A failure to record is one log line
/// and then silence for the session.
/// </para>
/// </remarks>
internal sealed class HandoverLedger
{
    private readonly Func<bool> _enabled;
    private readonly Func<bool> _isHost;
    private readonly LedgerFile _file;
    private readonly Action<string> _log;
    private readonly Action<string> _warn;

    /// <summary>
    /// The popup reading waiting for the server-side event that closes its row.
    /// One at a time: a handover is not something two of happen at once, and
    /// holding a queue would be inventing a problem.
    /// </summary>
    private ShownPopup _pendingPopup;

    private int _pendingCustomer;

    private string _pendingCustomerName = string.Empty;

    /// <summary>
    /// Set by the first failure of any kind. Said once, and then the ledger is
    /// closed for the session: a recorder that fails on one handover fails on
    /// the next for the same reason, and the host needs to read that once rather
    /// than once a deal.
    /// </summary>
    private bool _broken;

    private bool _saidWhereItIs;

    public HandoverLedger(
        Func<bool> enabled,
        Func<bool> isHost,
        LedgerFile file,
        Action<string> log,
        Action<string> warn)
    {
        _enabled = enabled;
        _isHost = isHost;
        _file = file;
        _log = log;
        _warn = warn;
    }

    /// <summary>Whether a row would be written right now.</summary>
    private bool Recording => !_broken && _enabled() && _isHost() && !_file.Failed;

    /// <summary>
    /// What the player was shown. Held until the server-side event for the same
    /// customer arrives, which is the thing that knows what the game actually
    /// moved.
    /// </summary>
    /// <remarks>
    /// A popup that is never claimed — because the handover's server RPC never
    /// reached this machine — is written out on its own rather than dropped when
    /// the next one arrives. What the player saw is the most valuable thing the
    /// ledger has; losing it silently would waste the evening the ledger exists
    /// to make count.
    /// </remarks>
    public void Shown(
        Customer customer,
        float satisfaction,
        float relationshipDelta,
        float basePayment,
        Il2CppSystem.Collections.Generic.List<Contract.BonusPayment> bonuses)
    {
        try
        {
            if (!Recording)
            {
                return;
            }

            FlushUnclaimedPopup();

            var shown = new List<ShownBonus>();
            if (bonuses is not null)
            {
                for (int i = 0; i < bonuses.Count; i++)
                {
                    Contract.BonusPayment bonus = bonuses[i];
                    if (bonus is not null)
                    {
                        shown.Add(new ShownBonus(bonus.Title ?? string.Empty, bonus.Amount));
                    }
                }
            }

            _pendingPopup = new ShownPopup(basePayment, satisfaction, relationshipDelta, shown);
            _pendingCustomer = customer == null ? 0 : customer.GetInstanceID();
            _pendingCustomerName = NameOf(customer);
        }
        catch (Exception error)
        {
            Broke(error);
        }
    }

    /// <summary>
    /// The server-side handover, which is every handover this machine sees —
    /// including one a remote player did by hand, where no popup ever plays
    /// here. This closes the row and writes it.
    /// </summary>
    public void Settled(
        Customer customer,
        HandoverScreen.EHandoverOutcome outcome,
        Il2CppSystem.Collections.Generic.List<ItemInstance> items,
        bool handoverByPlayer,
        float totalPayment,
        ProductList productList,
        float satisfaction)
    {
        try
        {
            if (!Recording)
            {
                return;
            }

            var row = new HandoverLedgerRow
            {
                Seam = "server",
                CustomerName = NameOf(customer),
                HandoverByPlayer = handoverByPlayer,
                Outcome = outcome.ToString(),
                TotalPayment = totalPayment,
                Satisfaction = satisfaction,
            };

            Claim(row, customer);
            ReadContract(row, customer);
            ReadRequested(row, productList);
            ReadDelivered(row, items, productList);
            ReadWorld(row, customer);
            Predict(row);
            Write(row);
        }
        catch (Exception error)
        {
            Broke(error);
        }
    }

    /// <summary>
    /// Attach the popup reading if it belongs to this handover. Matched on the
    /// customer rather than taken on trust: a popup left over from something
    /// else attached to this row would be a fabricated measurement, which is
    /// worse than none.
    /// </summary>
    private void Claim(HandoverLedgerRow row, Customer customer)
    {
        if (_pendingPopup is null)
        {
            row.ShownUnavailable =
                "no deal completion popup played on this machine for this handover, "
                + "which is what a handover done by another player looks like from here";
            return;
        }

        int id = customer == null ? 0 : customer.GetInstanceID();
        if (id != 0 && id == _pendingCustomer)
        {
            row.Shown = _pendingPopup;
            row.Seam = "server+popup";
            Forget();
            return;
        }

        // Someone else's popup. Write it out on its own so it is not lost, then
        // say that this row has none.
        FlushUnclaimedPopup();
        row.ShownUnavailable = "the popup that was waiting belonged to a different customer";
    }

    private void ReadContract(HandoverLedgerRow row, Customer customer)
    {
        try
        {
            row.ExcessProductsMatchSumMultiplier = Contract.ExcessProductsMatchSumMultiplier;

            Contract contract = customer == null ? null : customer.CurrentContract;
            if (contract == null)
            {
                // The popup carries the same figure, and it is the same field.
                row.ContractPayment = row.Shown?.BasePayment;
                row.World.Unreadable.Add("contract");
                return;
            }

            row.ContractPayment = contract.Payment;
            row.ContractKey = contract.GUID.ToString();
        }
        catch (Exception error)
        {
            row.World.Unreadable.Add("contract: " + error.Message);
        }
    }

    private static void ReadRequested(HandoverLedgerRow row, ProductList productList)
    {
        try
        {
            if (productList == null)
            {
                row.World.Unreadable.Add("product_list");
                return;
            }

            row.RequestedTotalQuantity = productList.GetTotalQuantity();

            Il2CppSystem.Collections.Generic.List<ProductList.Entry> entries = productList.entries;
            if (entries is null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ProductList.Entry entry = entries[i];
                if (entry is not null)
                {
                    row.Requested.Add(new RequestedLine(
                        entry.ProductID ?? string.Empty,
                        (int)entry.Quality,
                        entry.Quantity));
                }
            }
        }
        catch (Exception error)
        {
            row.World.Unreadable.Add("requested: " + error.Message);
        }
    }

    /// <summary>
    /// What was handed over, per item, and the two figures that follow from it:
    /// the units delivered and the mean tier difference.
    /// </summary>
    /// <remarks>
    /// The mean is the one place the ledger does arithmetic on a measurement,
    /// and it is the game's own: <c>EvaluateDelivery</c> sums
    /// <c>delivered.Quality - requested.Quality</c> over the delivered items and
    /// divides by the item count. It is recorded because the exceeded-quality
    /// bonus is a function of it, and a row that carried the bonus without it
    /// could not be checked.
    /// </remarks>
    private static void ReadDelivered(
        HandoverLedgerRow row,
        Il2CppSystem.Collections.Generic.List<ItemInstance> items,
        ProductList productList)
    {
        try
        {
            if (items is null)
            {
                row.World.Unreadable.Add("delivered_items");
                return;
            }

            Dictionary<string, int> requestedQuality = RequestedQualities(productList);
            int units = 0;
            int tierSum = 0;
            int tierCount = 0;

            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance item = items[i];
                if (item is null)
                {
                    continue;
                }

                ProductItemInstance product = item.TryCast<ProductItemInstance>();
                if (product is null)
                {
                    // Not product, so it earns nothing and is not part of the
                    // quality mean. Still worth a line: an item that should not
                    // be in a handover is exactly the sort of thing a ledger is
                    // for.
                    row.Delivered.Add(new DeliveredLine(
                        item.Definition == null ? "(unknown)" : item.Definition.ID ?? "(unknown)",
                        QualityTier.Lowest,
                        Amount: 0,
                        Quantity: 0,
                        TotalUnits: 0,
                        Packaging: null));
                    continue;
                }

                string id = product.ID ?? string.Empty;
                int quality = (int)product.Quality;
                int total = product.GetTotalAmount();

                row.Delivered.Add(new DeliveredLine(
                    id,
                    quality,
                    product.Amount,
                    product.Quantity,
                    total,
                    product.AppliedPackaging == null ? null : product.AppliedPackaging.ID));

                units += total;

                if (requestedQuality.TryGetValue(id, out int asked))
                {
                    tierSum += quality - asked;
                    tierCount++;
                }
            }

            row.DeliveredUnits = units;
            if (tierCount > 0)
            {
                row.QualityTiers = (float)tierSum / tierCount;
            }
        }
        catch (Exception error)
        {
            row.World.Unreadable.Add("delivered: " + error.Message);
        }
    }

    private static Dictionary<string, int> RequestedQualities(ProductList productList)
    {
        var qualities = new Dictionary<string, int>(StringComparer.Ordinal);

        if (productList == null)
        {
            return qualities;
        }

        Il2CppSystem.Collections.Generic.List<ProductList.Entry> entries = productList.entries;
        if (entries is null)
        {
            return qualities;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ProductList.Entry entry = entries[i];
            if (entry is not null && !string.IsNullOrEmpty(entry.ProductID))
            {
                qualities[entry.ProductID] = (int)entry.Quality;
            }
        }

        return qualities;
    }

    /// <summary>
    /// The world, because four of the five bonuses depend on it. Each reading is
    /// taken on its own and a failure names itself in
    /// <see cref="WorldReading.Unreadable"/> rather than costing the others.
    /// </summary>
    private static void ReadWorld(HandoverLedgerRow row, Customer customer)
    {
        WorldReading world = row.World;
        TimeManager time = null;

        try
        {
            if (NetworkSingleton<TimeManager>.InstanceExists)
            {
                time = NetworkSingleton<TimeManager>.Instance;
            }

            if (time == null)
            {
                world.Unreadable.Add("time");
            }
            else
            {
                world.GameTime = time.CurrentTime;
                world.DayIndex = time.DayIndex;
            }
        }
        catch (Exception error)
        {
            world.Unreadable.Add("time: " + error.Message);
        }

        try
        {
            Contract contract = customer == null ? null : customer.CurrentContract;
            QuestWindowConfig window = contract == null ? null : contract.DeliveryWindow;

            if (window == null)
            {
                world.Unreadable.Add("delivery_window");
            }
            else
            {
                world.WindowStart = window.WindowStartTime;
                world.WindowEnd = window.WindowEndTime;

                if (world.GameTime.HasValue)
                {
                    world.MinutesSinceWindowOpened =
                        TimeManager.GetMinSumFrom24HourTime(world.GameTime.Value)
                        - TimeManager.GetMinSumFrom24HourTime(window.WindowStartTime);
                }

                if (time != null && contract != null)
                {
                    // The game's own predicate for its own bonus, asked rather
                    // than reimplemented: start at the contract's accept time,
                    // end an hour after the window opens.
                    GameDateTime start = contract.AcceptTime;
                    var end = new GameDateTime(
                        start.elapsedDays,
                        TimeManager.AddMinutesTo24HourTime(window.WindowStartTime, 60));
                    world.WithinQuickWindow = time.IsCurrentDateWithinRange(start, end);
                }
            }
        }
        catch (Exception error)
        {
            world.Unreadable.Add("delivery_window: " + error.Message);
        }

        try
        {
            if (NetworkSingleton<CurfewManager>.InstanceExists)
            {
                CurfewManager curfew = NetworkSingleton<CurfewManager>.Instance;
                world.CurfewActive = curfew != null && curfew.IsCurrentlyActive;
            }
            else
            {
                world.Unreadable.Add("curfew");
            }
        }
        catch (Exception error)
        {
            world.Unreadable.Add("curfew: " + error.Message);
        }

        NPC npc = null;
        try
        {
            npc = customer == null ? null : customer.NPC;

            // The customer's own weather, which is the float the game's fifth
            // bonus reads. The method name's spelling is the game's.
            Il2CppScheduleOne.Core.Weather.WeatherConditions conditions =
                npc == null ? null : npc.GetCurrentWeatherConditionsForEnitty();

            if (conditions is null)
            {
                world.Unreadable.Add("weather");
            }
            else
            {
                world.Rainy = conditions.Rainy;
            }
        }
        catch (Exception error)
        {
            world.Unreadable.Add("weather: " + error.Message);
        }

        try
        {
            NPCRelationData relation = npc == null ? null : npc.RelationData;
            if (relation == null)
            {
                world.Unreadable.Add("relation");
            }
            else
            {
                world.RelationDelta = relation.RelationDelta;
                world.NormalizedRelationDelta = relation.NormalizedRelationDelta;
            }
        }
        catch (Exception error)
        {
            world.Unreadable.Add("relation: " + error.Message);
        }
    }

    /// <summary>
    /// Ours, and the only thing in the row that is. Left off entirely when the
    /// figures it needs were not measured, because a prediction made from
    /// guessed inputs would be indistinguishable in the file from one made from
    /// real ones.
    /// </summary>
    private static void Predict(HandoverLedgerRow row)
    {
        if (!row.ContractPayment.HasValue
            || !row.RequestedTotalQuantity.HasValue
            || !row.DeliveredUnits.HasValue
            || !row.Satisfaction.HasValue)
        {
            return;
        }

        row.Prediction = HandoverPrediction.Of(
            row.ContractPayment.Value,
            row.RequestedTotalQuantity.Value,
            row.DeliveredUnits.Value,
            row.Satisfaction.Value,
            row.QualityTiers,
            row.World.CurfewActive,
            row.World.WithinQuickWindow,
            row.World.Rainy);
    }

    /// <summary>
    /// Write a popup nobody claimed as a row of its own, so what the player saw
    /// survives even when the server side of the same handover never arrived.
    /// </summary>
    private void FlushUnclaimedPopup()
    {
        if (_pendingPopup is null)
        {
            return;
        }

        var row = new HandoverLedgerRow
        {
            Seam = "popup",
            CustomerName = _pendingCustomerName,
            Shown = _pendingPopup,
            ContractPayment = _pendingPopup.BasePayment,
            Satisfaction = _pendingPopup.Satisfaction,
        };

        row.World.Unreadable.Add("server_side_handover_never_arrived");
        Forget();
        Write(row);
    }

    private void Forget()
    {
        _pendingPopup = null;
        _pendingCustomer = 0;
        _pendingCustomerName = string.Empty;
    }

    private void Write(HandoverLedgerRow row)
    {
        if (_file.Append(row.ToJson()))
        {
            if (!_saidWhereItIs)
            {
                _saidWhereItIs = true;
                _log($"Recording handovers to {_file.Path}.");
            }

            return;
        }

        SayItFailed();
    }

    private void Broke(Exception error) =>
        Stop($"Dealcraft could not record a handover ({error.Message}).");

    private void SayItFailed() =>
        Stop($"Dealcraft could not write the handover ledger ({_file.Failure}).");

    /// <summary>
    /// Give up, once, out loud. The sentence is deliberate: a player reading the
    /// log needs to know first that nothing happened to their deal.
    /// </summary>
    private void Stop(string what)
    {
        if (_broken)
        {
            return;
        }

        _broken = true;
        _warn(what + " Handovers themselves are unaffected. The ledger has stopped "
            + "recording for this session; restart the game to try again.");
    }

    private static string NameOf(Customer customer)
    {
        try
        {
            NPC npc = customer == null ? null : customer.NPC;
            return npc == null ? string.Empty : npc.FullName ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
