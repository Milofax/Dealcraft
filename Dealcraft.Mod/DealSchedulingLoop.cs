using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Quests;
using UnityEngine;

namespace Dealcraft;

/// <summary>
/// Accepts standing offers into the deal windows the host allows.
/// </summary>
/// <remarks>
/// <para>
/// Server only. Six players may each have this mod installed and the game
/// replicates the same customer state to all of them; without the gate, six
/// machines act on one contract. Every contract is claimed through
/// <see cref="ContractClaimRegistry"/> before anything is sent and released
/// when the offer ends, and a contract a human has touched is locked out for
/// good.
/// </para>
/// <para>
/// The accept goes through <c>Customer.PlayerAcceptedContract(EDealWindow)</c>,
/// which is the method the player's own window selector calls and which ends in
/// <c>SendContractAccepted(window, trackContract: true)</c>. That was read out
/// of the shipped machine code — see <c>.scratch/worker-schedule-logbuch.md</c>
/// — and it is why a human and the automation cannot produce different results
/// from the same choice. No new NetworkObject, RPC type, prefab or asset is
/// introduced; the vanilla observer RPC carries the result to every client,
/// modded or not.
/// </para>
/// </remarks>
internal sealed class DealSchedulingLoop
{
    /// <summary>
    /// How long to wait between sweeps of the customer list. Offers live for
    /// game-hours, so this is about not searching the scene constantly rather
    /// than about reacting quickly.
    /// </summary>
    private const float SweepIntervalSeconds = 5f;

    /// <summary>
    /// How many customers one frame looks at. A sweep is spread across frames
    /// so that a session with a long customer list never costs a visible hitch.
    /// </summary>
    private const int CustomersPerFrame = 4;

    private readonly ContractClaimRegistry _claims;
    private readonly DealWindowRotation _rotation = new();
    private readonly DealWindowReader _windows;
    private readonly LifecycleWatch _lifecycle;
    private readonly Func<SchedulingConfiguration> _configuration;
    private readonly Action<string> _log;
    private readonly Action<string> _warn;

    /// <summary>
    /// Every contract key seen alive during the sweep in progress. Handed to
    /// the registry at the end of the sweep so its memory stays flat and a
    /// reload cannot leave it holding contracts the game has forgotten.
    /// </summary>
    private readonly List<string> _live = new();

    /// <summary>
    /// Reasons already reported, so a customer that is skipped every sweep for
    /// the same reason costs one line rather than one a second.
    /// </summary>
    private readonly Dictionary<string, string> _said = new(StringComparer.Ordinal);

    /// <summary>
    /// Reports each accepted deal once the game has given its contract real
    /// clock times, which is not in the frame the accept was made.
    /// </summary>
    private readonly AcceptedDealReporter _accepted;

    private bool _sweeping;
    private bool _announced;
    private int _cursor;
    private float _nextSweep;

    /// <param name="lifecycle">
    /// The mod's one reading of whether it may act at all. Asked before every
    /// sweep.
    /// </param>
    public DealSchedulingLoop(
        ContractClaimRegistry claims,
        LifecycleWatch lifecycle,
        Func<SchedulingConfiguration> configuration,
        Action<string> log,
        Action<string> warn)
    {
        _claims = claims;
        _lifecycle = lifecycle;
        _configuration = configuration;
        _log = log;
        _warn = warn;
        _windows = new DealWindowReader(warn);
        _accepted = new AcceptedDealReporter(log, HoursOf);
    }

    /// <summary>
    /// The scene changed, so everything cached in it is gone and everything
    /// said about it is stale.
    /// </summary>
    public void SceneChanged()
    {
        _windows.Forget();
        _said.Clear();
        _live.Clear();
        _accepted.Forget();
        _announced = false;
        _sweeping = false;
        _cursor = 0;
        _nextSweep = 0f;
    }

    /// <summary>The deal windows as the game reports them, for the app to show.</summary>
    public IReadOnlyList<DealWindowHours> WindowHours() => _windows.Hours();

    /// <summary>Whether this pass may act at all.</summary>
    private LifecycleVerdict Lifecycle(bool enabled) =>
        LifecycleWatch.Ask(_lifecycle, LifecyclePass.Scheduling, enabled);

    /// <summary>
    /// Say what the game's deal windows actually are, once per scene. A host
    /// who has allowed "Late Night" can then read in the log which hours that
    /// is, without opening anything.
    /// </summary>
    private void AnnounceWindows(DealWindowSet allowed)
    {
        IReadOnlyList<DealWindowHours> hours = _windows.Hours();
        if (hours.Count == 0 || _announced)
        {
            return;
        }

        _announced = true;
        _log($"The game reports {_windows.Shape()}.");
        for (int i = 0; i < hours.Count; i++)
        {
            _log($"  {hours[i]} — {(allowed.Allows(hours[i].Window) ? "allowed" : "not allowed")}");
        }
    }

    public void Tick()
    {
        try
        {
            _accepted.Tick();
            Sweep();
        }
        catch (Exception error)
        {
            // Never throw into the game's update loop. A sweep that failed
            // halfway is abandoned rather than resumed from an unknown place.
            _sweeping = false;
            _cursor = 0;
            _nextSweep = Time.realtimeSinceStartup + SweepIntervalSeconds;
            Once("sweep", $"the scheduling sweep failed and was abandoned ({error.Message})", warn: true);
        }
    }

    private void Sweep()
    {
        SchedulingConfiguration configuration = _configuration();

        // Before anything: may this machine act at all right now? The gate
        // answers the switch, the save, the load and the server in one place,
        // and it is where "never act while SaveManager.IsSaving" is enforced for
        // this pass — a sweep that accepted a deal into the middle of a save is
        // how the original earned its save-state bugs.
        LifecycleVerdict verdict = Lifecycle(configuration.Enabled);
        if (verdict.Outcome != LifecycleOutcome.Act)
        {
            if (verdict.Outcome == LifecycleOutcome.StandDown)
            {
                // A standing condition. Let go of the half-walked customer list
                // rather than holding a cursor into a list that may be gone by
                // the time the condition lifts.
                _sweeping = false;
                _cursor = 0;
                _live.Clear();
            }

            // A passing condition — a save being written — keeps the sweep where
            // it is and picks it up again next tick.
            if (verdict.WorthReporting)
            {
                Once(LifecyclePass.Scheduling, verdict.Reason);
            }

            return;
        }

        if (!_sweeping)
        {
            if (Time.realtimeSinceStartup < _nextSweep)
            {
                return;
            }

            _sweeping = true;
            _cursor = 0;
            _live.Clear();
            AnnounceWindows(configuration.Windows);
        }

        // A player at the picker is choosing a window by hand right now. The
        // mod does not answer for them, and the vanilla selector does not say
        // whose offer it holds, so the whole sweep waits.
        if (_windows.APlayerIsChoosing())
        {
            Once("picker", "a player has the deal window picker open, so scheduling is standing by");
            EndSweep(reconcile: false);
            return;
        }

        LockOutWhateverAPlayerHasOpen();

        Il2CppSystem.Collections.Generic.List<Customer> customers = Customer.UnlockedCustomers;
        if (customers is null)
        {
            EndSweep(reconcile: false);
            return;
        }

        IReadOnlyList<DealWindow> onOffer = _windows.OnOffer();

        // Where the walk over the windows begins. Read here rather than per
        // customer so that every offer answered in one pass counts from the
        // same minute, and read from the same place the hours come from: the
        // game's clock and the game's own clock-to-window mapping.
        DealWindow? now = _windows.CurrentWindow();

        int examined = 0;
        while (_cursor < customers.Count && examined < CustomersPerFrame)
        {
            Customer customer = customers[_cursor];
            _cursor++;
            examined++;

            if (customer == null)
            {
                continue;
            }

            Examine(customer, configuration, onOffer, now);
        }

        if (_cursor >= customers.Count)
        {
            EndSweep(reconcile: true);
        }
    }

    private void EndSweep(bool reconcile)
    {
        if (reconcile)
        {
            // Only after a whole sweep: a partial list would forget contracts
            // that are still live and let them be claimed a second time.
            _claims.Reconcile(_live);
            ForgetWhatIsNoLongerWorthSaying();
        }

        _sweeping = false;
        _cursor = 0;
        _live.Clear();
        _nextSweep = Time.realtimeSinceStartup + SweepIntervalSeconds;
    }

    private void Examine(
        Customer customer,
        SchedulingConfiguration configuration,
        IReadOnlyList<DealWindow> onOffer,
        DealWindow? now)
    {
        PendingOffer offer = ReadOffer(customer);

        if (offer.HasOfferedContract && !string.IsNullOrWhiteSpace(offer.ContractKey))
        {
            _live.Add(offer.ContractKey);
        }

        ScheduleDecision eligibility = DealScheduler.Consider(
            offer, ServerAuthorityReader.Read(customer), configuration.Enabled);

        if (eligibility.Outcome != ScheduleOutcome.Claim)
        {
            // Only worth a line for a customer who actually has an offer: the
            // rest of the list is not news.
            if (offer.HasOfferedContract)
            {
                Once(Subject(offer), eligibility.Reason);
                Record(offer, eligibility);
            }

            return;
        }

        // Look before claiming. A claim spent on a deal that then had nowhere
        // to go would lock that offer out for the rest of its life.
        ScheduleDecision available = _rotation.Peek(configuration.Windows, onOffer, now);
        if (available.Outcome != ScheduleOutcome.Schedule)
        {
            Once(Subject(offer), $"{offer.CustomerName}: {available.Reason}");
            Record(offer, available);
            return;
        }

        ScheduleDecision chosen = DealScheduler.ChooseWindow(
            _claims.Claim(offer.ContractKey), configuration.Windows, onOffer, now, _rotation);

        if (chosen.Outcome != ScheduleOutcome.Schedule)
        {
            Once(Subject(offer), $"{offer.CustomerName}: {chosen.Reason}");
            Record(offer, chosen);
            return;
        }

        // Recorded inside Accept rather than here: this is where the window is
        // chosen, not where the deal is taken, and the game's own call can
        // still refuse it.
        Accept(customer, offer, chosen, configuration);
    }

    /// <summary>
    /// One line of the debug record: which window this offer went into, or why
    /// none did. Written for the refusals too — "no window you allow was free"
    /// is invisible from inside the game, and it is the answer to the question
    /// a player actually asks, which is why their deal was never accepted.
    /// </summary>
    private static void Record(in PendingOffer offer, in ScheduleDecision decision, bool acted = false) =>
        DecisionRecord.Scheduling(
            decision.Outcome,
            acted,
            decision.Reason,
            offer.CustomerName,
            offer.ContractKey,
            decision.Outcome == ScheduleOutcome.Schedule ? decision.Window : null);

    private void Accept(
        Customer customer,
        PendingOffer offer,
        ScheduleDecision chosen,
        SchedulingConfiguration configuration)
    {
        DealWindow window = chosen.Window;

        try
        {
            // The player's own path: this sends the reply into the conversation,
            // clears the responses and performs SendContractAccepted(window,
            // trackContract: true). The server RPC is what every client sees.
            customer.PlayerAcceptedContract((EDealWindow)(int)window);
        }
        catch (Exception error)
        {
            // The claim stays, so a customer whose accept threw is not tried
            // again this session rather than being hammered every sweep.
            _warn($"{offer.CustomerName}: the deal could not be accepted ({error.Message})");
            Record(offer, ScheduleDecision.Skip(
                $"{DealWindowName.Of(window)} was chosen, and the game refused the accept "
                    + $"({error.Message})"));
            return;
        }

        // The offer has ended, whatever became of the contract.
        _claims.Release(offer.ContractKey);
        _said.Remove(offer.ContractKey);

        Record(offer, chosen, acted: true);
        _accepted.Accepted(customer, offer.CustomerName, window);
    }

    /// <summary>
    /// One window's hours as the game reports them. A window the game could not
    /// be asked about comes back zero to zero, which
    /// <see cref="DealWindowHours.Available"/> reads as no answer, so the report
    /// says the hours were not reported instead of spelling them as midnight.
    /// </summary>
    private DealWindowHours HoursOf(DealWindow window) =>
        DealWindowHours.Find(_windows.Hours(), window) ?? new DealWindowHours(window, 0, 0);

    /// <summary>
    /// Where the deals already in this window have to be delivered. Advisory
    /// only, and skipped entirely while the guard is off.
    /// </summary>
    private IReadOnlyList<string> DeliveriesAlreadyIn(DealWindow window)
    {
        var places = new List<string>();

        try
        {
            Il2CppSystem.Collections.Generic.List<Contract> contracts = Contract.Contracts;
            if (contracts is null)
            {
                return places;
            }

            for (int i = 0; i < contracts.Count; i++)
            {
                Contract contract = contracts[i];
                if (contract == null)
                {
                    continue;
                }

                QuestWindowConfig config = contract.DeliveryWindow;
                if (config is null || _windows.WindowOf(config.WindowStartTime) != window)
                {
                    continue;
                }

                DeliveryLocation location = contract.DeliveryLocation;
                if (location != null)
                {
                    places.Add(location.LocationName ?? string.Empty);
                }
            }
        }
        catch (Exception)
        {
            // An advisory that could not be gathered is simply not given.
            places.Clear();
        }

        return places;
    }

    /// <summary>
    /// A contract whose counteroffer screen a player has open is theirs, for
    /// good. The window picker cannot be attributed to a customer, so the sweep
    /// waits for that one instead; this is the case where the game does say
    /// whose offer is on screen.
    /// </summary>
    private void LockOutWhateverAPlayerHasOpen()
    {
        try
        {
            CounterofferLookup lookup = CounterofferScreen.Look();
            if (lookup.State != CounterofferState.Open)
            {
                return;
            }

            string key = OfferedContractKey.Of(lookup.Counteroffer.Customer);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _claims.NoteHumanInteraction(key);
            Once(key, "a player has this offer open, so the automation leaves it alone for good");
        }
        catch (Exception)
        {
            // A screen that closed mid-read locks nobody out, which is the
            // same as it having been closed all along.
        }
    }

    /// <summary>
    /// One customer's standing offer as plain values.
    /// </summary>
    internal static PendingOffer ReadOffer(Customer customer)
    {
        try
        {
            NPC npc = customer.NPC;
            string name = npc == null ? string.Empty : npc.FullName ?? string.Empty;

            return new PendingOffer(
                OfferedContractKey.Of(customer),
                name,
                hasOfferedContract: customer.OfferedContractInfo is not null,
                alreadyOnADeal: customer.CurrentContract != null);
        }
        catch (Exception)
        {
            // A customer that despawned mid-read has no offer worth acting on.
            return new PendingOffer(string.Empty, string.Empty, false, false);
        }
    }

    /// <summary>
    /// Drop what was said about offers that are no longer on the table, so a
    /// long session's memory of the log stays as flat as the claim registry's.
    /// </summary>
    private void ForgetWhatIsNoLongerWorthSaying()
    {
        var live = new HashSet<string>(_live, StringComparer.Ordinal);
        var stale = new List<string>();

        foreach (string subject in _said.Keys)
        {
            // Only contract keys are forgotten, and a contract key is the one
            // subject with a '#' in it. Everything else is a standing condition
            // or a customer, and saying either again every five seconds would
            // be worse than remembering it.
            if (subject.IndexOf('#') >= 0 && !live.Contains(subject))
            {
                stale.Add(subject);
            }
        }

        foreach (string subject in stale)
        {
            _said.Remove(subject);
        }
    }

    /// <summary>
    /// What a line about this offer is filed under. The contract key where
    /// there is one, and the customer's name where there is not — so a customer
    /// whose offer cannot be keyed still gets one line rather than trading
    /// places with every other keyless customer, sweep after sweep.
    /// </summary>
    private static string Subject(in PendingOffer offer)
    {
        if (!string.IsNullOrWhiteSpace(offer.ContractKey))
        {
            return offer.ContractKey;
        }

        return string.IsNullOrWhiteSpace(offer.CustomerName) ? "-" : offer.CustomerName;
    }

    /// <summary>Say a thing once per subject, however many sweeps repeat it.</summary>
    private void Once(string subject, string line, bool warn = false)
    {
        string key = string.IsNullOrWhiteSpace(subject) ? "-" : subject;

        if (_said.TryGetValue(key, out string said) && said == line)
        {
            return;
        }

        _said[key] = line;

        if (warn)
        {
            _warn(line);
        }
        else
        {
            _log(line);
        }
    }
}
