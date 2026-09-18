using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using UnityEngine;

namespace Dealcraft;

/// <summary>
/// Sends counter-offers at the best price the customer will still take, on the
/// host, once per contract.
/// </summary>
/// <remarks>
/// <para>
/// Server only. Six players may each have this mod installed and the game
/// replicates the same customer state to all of them; without the gate, six
/// machines counter one contract. Every contract is claimed through
/// <see cref="ContractClaimRegistry"/> before anything is sent, and a contract
/// a human has opened is locked out of the automation for good.
/// </para>
/// <para>
/// The counter goes through <c>Customer.SendCounteroffer(product, quantity,
/// price)</c>. That is the callback the vanilla counteroffer screen invokes when
/// the player presses Send — <c>Customer.CounterOfferClicked</c> (RVA 0x6A7100)
/// hands it to <c>CounterofferInterface.Open</c>, and <c>Send()</c> (RVA
/// 0x9DDFD0) calls it with the screen's quantity and its price selector's
/// amount. Its body (RVA 0x6B6A80) writes the player's reply into the
/// conversation, clears the responses and then performs — inlined —
/// <c>ProcessCounterOfferServerSide(productID, quantity, price)</c>, byte for
/// byte the RPC writer at RVA 0x6AEE90, which has no other call site in the
/// binary. So the automation reaches the ticket's RPC by the one route a player
/// reaches it, and the conversation is left in the state a player's press leaves
/// it in. The server's own <c>RpcLogic</c> answers through the vanilla
/// <c>SetContractIsCounterOffer</c> observer RPC and the customer's SMS reply,
/// which every client sees, modded or not. No new NetworkObject, RPC type,
/// prefab or asset.
/// </para>
/// <para>
/// The offer sent is <see cref="CounterofferSearch"/>'s planned offer, built
/// from the screen's own limits by the same call a player's hand would have gone
/// through. (The advisor overlay's Apply button was the other caller of that
/// path, and is deleted.)
/// </para>
/// </remarks>
internal sealed class CounterofferLoop
{
    /// <summary>
    /// How long to wait between sweeps of the customer list. Offers live for
    /// game-hours, so this is about not searching the scene constantly rather
    /// than about reacting quickly.
    /// </summary>
    private const float SweepIntervalSeconds = 5f;

    /// <summary>
    /// How many customers one frame looks at. Deliberately one: unlike the
    /// scheduler's pass, examining a customer here can mean building a whole
    /// price curve, which is a few hundred synchronous calls into the game. One
    /// per frame keeps the worst frame the cost of one curve.
    /// </summary>
    private const int CustomersPerFrame = 1;

    private readonly ContractClaimRegistry _claims;
    private readonly LifecycleWatch _lifecycle;
    private readonly Func<CounterofferConfiguration> _configuration;
    private readonly Action<string> _log;
    private readonly Action<string> _warn;

    /// <summary>
    /// Every contract key seen alive during the sweep in progress. Handed to
    /// the registry at the end of the sweep so its memory stays flat and a
    /// reload cannot leave it holding contracts the game has forgotten.
    /// </summary>
    private readonly List<string> _live = new();

    /// <summary>
    /// Reasons already reported about an offer, so a customer skipped every
    /// sweep for the same reason costs one line rather than one every five
    /// seconds. Forgotten along with the claim registry, so an offer that comes
    /// back is news again.
    /// </summary>
    private readonly SaidOnce _saidAboutOffers = new();

    /// <summary>
    /// The same, for the standing conditions that belong to no contract — a
    /// screen that cannot be read, a sweep that failed. Kept separately because
    /// these must <em>not</em> be forgotten when the contracts are: they are
    /// true until they stop being true, and repeating them every five seconds
    /// is exactly what saying a thing once is for.
    /// </summary>
    private readonly SaidOnce _saidAboutTheSweep = new();

    private bool _sweeping;
    private int _cursor;

    private float _nextSweep;

    /// <param name="lifecycle">
    /// The mod's one reading of whether it may act at all — the switch, the
    /// save, the load and the server. Asked before every sweep.
    /// </param>
    public CounterofferLoop(
        ContractClaimRegistry claims,
        LifecycleWatch lifecycle,
        Func<CounterofferConfiguration> configuration,
        Action<string> log,
        Action<string> warn)
    {
        _claims = claims;
        _lifecycle = lifecycle;
        _configuration = configuration;
        _log = log;
        _warn = warn;
    }

    /// <summary>
    /// The scene changed, so everything cached in it is gone and everything
    /// said about it is stale.
    /// </summary>
    public void SceneChanged()
    {
        _saidAboutOffers.Forget(Array.Empty<string>());
        _saidAboutTheSweep.Forget(Array.Empty<string>());
        _live.Clear();
        _sweeping = false;
        _cursor = 0;
        _nextSweep = 0f;
    }

    public void Tick()
    {
        try
        {
            Sweep();
        }
        catch (Exception error)
        {
            // Never throw into the game's update loop. A sweep that failed
            // halfway is abandoned rather than resumed from an unknown place.
            _sweeping = false;
            _cursor = 0;
            _nextSweep = Time.realtimeSinceStartup + SweepIntervalSeconds;
            AboutTheSweep(
                "sweep",
                $"the counter-offer sweep failed and was abandoned ({error.Message})",
                warn: true);
        }
    }

    private void Sweep()
    {
        CounterofferConfiguration configuration = _configuration();

        // Before anything: may this machine act at all right now? The gate
        // answers the switch, the save, the load and the server in one place.
        // Countering into the middle of a save writes a conversation the file
        // may or may not have got to, which is the failure this guard exists
        // for; a host who never turned counter-offers on still gets a quiet log,
        // because the gate says that verdict is not worth reporting.
        LifecycleVerdict verdict = LifecycleWatch.Ask(
            _lifecycle, LifecyclePass.Counteroffers, configuration.Enabled);
        if (verdict.Outcome != LifecycleOutcome.Act)
        {
            if (verdict.Outcome == LifecycleOutcome.StandDown)
            {
                _sweeping = false;
                _cursor = 0;
                _live.Clear();
            }

            if (verdict.WorthReporting)
            {
                AboutTheSweep(LifecyclePass.Counteroffers, verdict.Reason);
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
        }

        LockOutWhateverAPlayerHasOpen();

        if (!CounterofferControls.TryReadLimits(out CounterofferLimits limits, out string problem))
        {
            // No screen to read limits off means no idea what the game would
            // accept, and guessing is how an offer nobody could have made gets
            // sent. Wait for the next sweep instead.
            AboutTheSweep(
                "limits",
                $"the counteroffer screen's limits could not be read, so nothing is countered ({problem})");
            EndSweep(reconcile: false);
            return;
        }

        Il2CppSystem.Collections.Generic.List<Customer> customers = Customer.UnlockedCustomers;
        if (customers is null)
        {
            EndSweep(reconcile: false);
            return;
        }

        int examined = 0;
        while (_cursor < customers.Count && examined < CustomersPerFrame)
        {
            Customer customer = customers[_cursor];
            _cursor++;

            if (customer == null)
            {
                continue;
            }

            // Only a customer that cost something counts against the frame's
            // allowance; skipping a list of customers with nothing on the table
            // is not work worth spreading over frames.
            if (Examine(customer, configuration, limits))
            {
                examined++;
            }
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
            _saidAboutOffers.Forget(_live);
        }

        _sweeping = false;
        _cursor = 0;
        _live.Clear();
        _nextSweep = Time.realtimeSinceStartup + SweepIntervalSeconds;
    }

    /// <summary>
    /// Consider one customer. Answers whether anything expensive was done, so
    /// that a sweep spends its per-frame allowance on curves rather than on
    /// customers with nothing on the table.
    /// </summary>
    private bool Examine(
        Customer customer,
        CounterofferConfiguration configuration,
        in CounterofferLimits limits)
    {
        OfferedContract offered = OfferedContract.Read(customer);
        PendingOffer offer = offered.Offer;

        if (offer.HasOfferedContract && !string.IsNullOrWhiteSpace(offer.ContractKey))
        {
            _live.Add(offer.ContractKey);
        }

        CounterofferDecision eligibility = CounterofferGate.Consider(
            offer, ServerAuthorityReader.Read(customer), configuration.Enabled);

        if (eligibility.Outcome != CounterofferOutcome.Claim)
        {
            // Only worth a line for a customer who actually has an offer: the
            // rest of the list is not news.
            if (offer.HasOfferedContract)
            {
                AboutTheOffer(Subject(offer), eligibility.Reason);
                Record(offer, eligibility.Outcome, acted: false, eligibility.Reason, offered);
            }

            return false;
        }

        if (!offered.IsPriceable)
        {
            const string unpriceable = "the offered contract names no product this mod can price";
            AboutTheOffer(Subject(offer), $"{offer.CustomerName}: {unpriceable}");
            Record(offer, CounterofferOutcome.Skip, acted: false, unpriceable, offered);
            return false;
        }

        // Claim before asking the customer anything. A curve is a few hundred
        // synchronous calls into the game, and the answer is only worth having
        // if this machine is the one that may act on it.
        ClaimDecision claim = _claims.Claim(offer.ContractKey);

        if (claim.Outcome != ClaimOutcome.Claimed)
        {
            AboutTheOffer(Subject(offer), $"{offer.CustomerName}: {claim.Reason}");
            Record(offer, CounterofferOutcome.Skip, acted: false, claim.Reason, offered);
            return false;
        }

        Negotiation advice = CounterofferSearch.For(
            customer,
            offered.Product,
            limits,
            // The customer's own offered quantity is what is on the table when
            // no screen is open, and it is what gets priced while the host is
            // after the best price rather than a deal of their own size.
            offered.Quantity,

            // The same floor the gate below holds the answer against, handed in
            // so the search can aim at it rather than be judged by it.
            configuration.Settings.AcceptanceProbabilityThreshold,
            out ProbeDiagnosis probe);

        // The chance goes in, and that is the whole of the money fix: countering
        // spends the offer on the table, so a counter has to be worth more than
        // the certain money. CounterofferSearch computes this figure today and
        // the loop used to throw it away.
        //
        // The floor goes in beside it, and it is the player's own: the search
        // has already picked the rung that earns most, and the floor only
        // refuses that winner when it is a bigger gamble than the host asked to
        // take. It is read here, off the same sweep's settings, so a number
        // edited while the game runs applies to the next sweep.
        CounterofferDecision decision = CounterofferGate.Send(
            claim,
            advice.Planned,
            advice.Chance,
            offered.Payment,
            configuration.Settings.AcceptanceProbabilityThreshold);

        if (decision.Outcome != CounterofferOutcome.Send)
        {
            // The claim stays: an offer whose curve said no is not asked again
            // every five seconds. Reconciling forgets it when the offer goes.
            //
            // The probe's own account goes on the end of the line, because the
            // verdict on its own has been read wrongly once already: "no price
            // reached the confidence" reads as a threshold set too high, and the
            // recorded chance behind those words was zero at every price for
            // four customers who had asked to buy.
            AboutTheOffer(Subject(offer), Line(offer, decision.Reason, probe));

            // The expensive kind of nothing, and the row most worth having: the
            // curve was built, the chance was found, and the money still did
            // not beat the offer already on the table. So the search's figures
            // go in even though nothing was sent.
            Record(offer, decision.Outcome, acted: false, decision.Reason, offered, advice, probe);
            return true;
        }

        _log($"{offer.CustomerName}: {advice.Explanation}");
        Send(customer, offered, decision, advice, probe);
        return true;
    }

    /// <summary>
    /// One line of the debug record: what this offer came to, and why. Written
    /// for every verdict including the ones that do nothing, because from
    /// inside the game a player cannot see why an offer was left alone.
    /// </summary>
    /// <param name="advice">
    /// The search's answer, where a search ran. Absent for every verdict the
    /// gate reached before the curve was built; the row then says so with a
    /// null rather than with a zero, because zero is a price somebody could
    /// have meant and "we never asked" is not.
    /// </param>
    /// <param name="probe">
    /// What the probing came to, where a search ran. The chance beside it is the
    /// search's verdict and this is its working: which item list the game was
    /// handed, what it answered at the first price and the last, and anything
    /// that threw on the way. Absent for the same rows the advice is absent for.
    /// </param>
    private static void Record(
        in PendingOffer offer,
        CounterofferOutcome outcome,
        bool acted,
        string reason,
        in OfferedContract offered,
        Negotiation? advice = null,
        ProbeDiagnosis probe = null)
    {
        // A search that ran and planned nothing still found a chance, and that
        // is the figure a refusal is argued from — so the chance goes in
        // whenever a search ran, and the quantity and the price only when
        // there was something to send.
        bool planned = advice.HasValue && advice.Value.Planned.HasOffer;

        DecisionRecord.Negotiation(
            outcome,
            acted,
            reason,
            offer.CustomerName,
            offer.ContractKey,
            offered.ProductId,
            planned ? advice.Value.Planned.Quantity : null,
            planned ? advice.Value.Planned.TotalPrice : null,
            advice?.Chance,
            offer.HasOfferedContract ? offered.Payment : null,
            probe?.Summary());
    }

    /// <summary>
    /// One line of the log about an offer, with the probe's account on the end
    /// where there is one. The record is what gets read back, but the owner is
    /// playing while this happens and the log is what he can see.
    /// </summary>
    private static string Line(in PendingOffer offer, string reason, ProbeDiagnosis probe)
    {
        string said = probe?.Summary();

        return string.IsNullOrEmpty(said)
            ? $"{offer.CustomerName}: {reason}"
            : $"{offer.CustomerName}: {reason} — {said}";
    }

    private void Send(
        Customer customer,
        in OfferedContract offered,
        in CounterofferDecision decision,
        in Negotiation advice,
        ProbeDiagnosis probe)
    {
        string who = offered.Offer.CustomerName;

        try
        {
            // The player's own path: this sends the reply into the conversation,
            // clears the responses and performs the counter-offer server RPC.
            // The vanilla observer RPC is what every client sees.
            customer.SendCounteroffer(offered.Product, decision.Quantity, decision.TotalPrice);
        }
        catch (Exception error)
        {
            // The claim stays, so a customer whose counter threw is not tried
            // again this session rather than being hammered every sweep.
            _warn($"{who}: the counter-offer could not be sent ({error.Message})");
            Record(
                offered.Offer,
                CounterofferOutcome.Skip,
                acted: false,
                $"the counter was worked out and the game refused to send it ({error.Message})",
                offered,
                advice,
                probe);
            return;
        }

        // The automation is done with this contract. The entry stays until the
        // offer leaves the game, so it cannot be claimed a second time; the
        // game's own IsCounterOffer covers the same ground across a reload,
        // when the registry starts empty again.
        _claims.Release(offered.Offer.ContractKey);

        _log($"{who}: countered with {decision.Quantity} of {offered.ProductId} at "
            + $"{GameMoney.Rounded(decision.TotalPrice)} (offered {GameMoney.Rounded(offered.Payment)})");

        Record(offered.Offer, decision.Outcome, acted: true, decision.Reason, offered, advice, probe);
    }

    /// <summary>
    /// A contract whose counteroffer screen a player has open is theirs, for
    /// good. This is the case where the game does say whose offer is on screen.
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
            AboutTheOffer(key, "a player has this offer open, so the automation leaves it alone for good");
        }
        catch (Exception)
        {
            // A screen that closed mid-read locks nobody out, which is the same
            // as it having been closed all along.
        }
    }

    /// <summary>
    /// What a line about this offer is filed under: the contract key where
    /// there is one, the customer's name where there is not.
    /// </summary>
    private static string Subject(in PendingOffer offer)
    {
        if (!string.IsNullOrWhiteSpace(offer.ContractKey))
        {
            return offer.ContractKey;
        }

        return string.IsNullOrWhiteSpace(offer.CustomerName) ? "-" : offer.CustomerName;
    }

    /// <summary>Say a thing about an offer once, however many sweeps repeat it.</summary>
    private void AboutTheOffer(string subject, string line)
    {
        if (_saidAboutOffers.ShouldSay(subject, line))
        {
            _log(line);
        }
    }

    /// <summary>Say a thing about the sweep itself once, and keep it said.</summary>
    private void AboutTheSweep(string subject, string line, bool warn = false)
    {
        if (!_saidAboutTheSweep.ShouldSay(subject, line))
        {
            return;
        }

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
