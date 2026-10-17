using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Quests;
using UnityEngine;

namespace Dealcraft;

/// <summary>
/// Reports each accepted deal with the clock times the game gives its contract.
/// </summary>
/// <remarks>
/// Accepting sends a server RPC, so the contract it creates does not exist in
/// the frame the accept was made. Reporting straight away would mean announcing
/// that the game reported no timings, every time. So the report waits for the
/// contract, and falls back — after a bounded wait — on the delivery window the
/// game wrote into the offer as the accept went out, which is the same window
/// the contract will carry.
/// </remarks>
internal sealed class AcceptedDealReporter
{
    /// <summary>
    /// How long a deal waits for its contract. Long enough for a round trip on
    /// a bad connection, short enough that the log still reads as a reaction to
    /// what just happened.
    /// </summary>
    private const float WaitSeconds = 5f;

    private readonly Action<string> _log;
    private readonly Func<DealWindow, DealWindowHours> _hoursOf;
    private readonly List<Awaited> _awaiting = new();

    /// <param name="hoursOf">
    /// The window's hours as the game reports them, for the line that names the
    /// window.
    /// </param>
    public AcceptedDealReporter(Action<string> log, Func<DealWindow, DealWindowHours> hoursOf)
    {
        _log = log;
        _hoursOf = hoursOf;
    }

    /// <summary>Nothing outstanding survives a scene change.</summary>
    public void Forget() => _awaiting.Clear();

    /// <summary>
    /// A deal has just been accepted. Called straight after the accept, while
    /// the offer's delivery window is still readable.
    /// </summary>
    public void Accepted(Customer customer, string customerName, DealWindow window)
    {
        _awaiting.Add(new Awaited(
            customer,
            customerName,
            window,
            OfferedWindowOf(customer),
            Time.realtimeSinceStartup + WaitSeconds));
    }

    /// <summary>
    /// Report every deal whose contract the game has now created, and give up
    /// on any that has waited long enough.
    /// </summary>
    public void Tick()
    {
        for (int i = _awaiting.Count - 1; i >= 0; i--)
        {
            Awaited awaited = _awaiting[i];
            QuestWindowConfig window = DeliveryWindowOf(awaited.Customer);

            if (window is null && Time.realtimeSinceStartup < awaited.GiveUpAt)
            {
                continue;
            }

            _awaiting.RemoveAt(i);
            Report(awaited, window ?? awaited.OfferedWindow);
        }
    }

    private void Report(Awaited awaited, QuestWindowConfig window)
    {
        foreach (string line in ScheduledDealReport.Describe(
                     awaited.CustomerName, _hoursOf(awaited.Window), TimingsOf(window)))
        {
            _log(line);
        }
    }

    /// <summary>
    /// The contract's real timings, asked of the game rather than worked out
    /// from the window. <c>GetContractTimings</c> reads the window's own start
    /// and end and adds the customer's travel allowance to the start; nothing
    /// here restates that.
    /// </summary>
    private static ContractTimings TimingsOf(QuestWindowConfig window)
    {
        if (window is null)
        {
            return ContractTimings.Unavailable;
        }

        try
        {
            Customer.GetContractTimings(window, out int soft, out int hard, out int end);
            return new ContractTimings(soft, hard, end);
        }
        catch (Exception)
        {
            return ContractTimings.Unavailable;
        }
    }

    /// <summary>
    /// The delivery window the game wrote into the offer as the accept went
    /// out. It is the same window config the contract will carry and it is
    /// readable straight away, so a contract that never turns up still gets
    /// real timings rather than none.
    /// </summary>
    private static QuestWindowConfig OfferedWindowOf(Customer customer)
    {
        try
        {
            ContractInfo offered = customer.OfferedContractInfo;
            return offered is null ? null : offered.DeliveryWindow;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The accepted contract's own delivery window, once it exists.</summary>
    private static QuestWindowConfig DeliveryWindowOf(Customer customer)
    {
        try
        {
            Contract contract = customer == null ? null : customer.CurrentContract;
            return contract == null ? null : contract.DeliveryWindow;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>A deal accepted and waiting for the game to create its contract.</summary>
    private readonly struct Awaited
    {
        public Awaited(
            Customer customer,
            string customerName,
            DealWindow window,
            QuestWindowConfig offeredWindow,
            float giveUpAt)
        {
            Customer = customer;
            CustomerName = customerName;
            Window = window;
            OfferedWindow = offeredWindow;
            GiveUpAt = giveUpAt;
        }

        public Customer Customer { get; }

        public string CustomerName { get; }

        /// <summary>The window the automation chose.</summary>
        public DealWindow Window { get; }

        /// <summary>
        /// The window config the game wrote into the offer, used when the
        /// contract itself never turns up.
        /// </summary>
        public QuestWindowConfig OfferedWindow { get; }

        public float GiveUpAt { get; }
    }
}
