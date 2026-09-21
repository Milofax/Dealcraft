using System;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Handover;
using UnityEngine;

namespace Dealcraft;

/// <summary>
/// What every line of one contract would be delivered as, or why none of it can
/// be. All or nothing: a contract is one order, and half of one is worse than a
/// late one.
/// </summary>
internal readonly struct DeliveryPlan
{
    private DeliveryPlan(IReadOnlyList<GradePlan> plans, string why)
    {
        Plans = plans;
        Why = why;
    }

    public IReadOnlyList<GradePlan> Plans { get; }

    /// <summary>Why nothing can be delivered. Empty when something can.</summary>
    public string Why { get; }

    public bool CanDeliver => Plans is { Count: > 0 };

    public static DeliveryPlan Deliver(IReadOnlyList<GradePlan> plans) =>
        new(plans, string.Empty);

    public static DeliveryPlan Nothing(string why) =>
        new(Array.Empty<GradePlan>(), why);
}

/// <summary>
/// One line of a contract as the bag actually answered it: the grade taken, how
/// many whole packages that was, and the units they came to.
/// </summary>
/// <remarks>
/// Read after <c>HandoverGoods.Take</c> rather than off the plan, because the
/// two differ whenever packaging forces an overshoot — packages cannot be split
/// (<c>PackagePlan.cs:23-24</c>) — and the record is supposed to say what left
/// the player's pockets.
/// </remarks>
internal readonly struct DeliveredGrade
{
    public DeliveredGrade(string productId, int grade, int packages, int units)
    {
        ProductId = productId;
        Grade = grade;
        Packages = packages;
        Units = units;
    }

    public string ProductId { get; }

    public int Grade { get; }

    public int Packages { get; }

    public int Units { get; }
}

/// <summary>
/// Completes a handover when the game says it is valid.
///
/// <para><b>Once, by whoever is carrying the goods.</b> A handover is not
/// shared state: it is one player's product leaving one player's pockets into
/// the customer standing in front of <em>them</em>, and the game already asks it
/// that way — <c>Customer.IsReadyForHandover</c> reads the local player
/// singleton, so on every machine it answers about that machine's own player.
/// Two players cannot both be the one carrying the goods, which is why this pass
/// needs no host rule; see <see cref="HandoverGate"/>.</para>
///
/// <para><b>Within one instance</b>, <see cref="ContractClaimRegistry"/> still
/// claims every contract before a single call is made, so a scan that comes
/// round again while a handover is in flight does not start a second one. Across
/// instances it does not need to: the game's own
/// <c>Customer.IsHandoverChoiceValid</c> and the vanilla server RPC settle two
/// players racing exactly as they settle two players racing the Done button
/// today.</para>
///
/// <para><b>Through the game's own call.</b> The handover goes out through
/// <c>Customer.ProcessHandover</c>, which is the vanilla method the handover
/// screen's Done button reaches and whose entire job is to work out the seven
/// arguments of <c>ProcessHandoverServerSide</c> and send it. Calling the server
/// RPC directly would mean reimplementing the game's five-bonus payment
/// arithmetic — and would skip <c>Contract.SubmitPayment</c>, which is where the
/// money actually reaches the player, so every automated deal would be done for
/// free. See the project notes for the reading this rests
/// on.</para>
///
/// <para>No new network type, no prefab, no asset. An unmodified client sees an
/// ordinary handover arrive through <c>ProcessHandoverClient</c>.</para>
/// </summary>
internal sealed class AutomaticHandover
{
    /// <summary>
    /// How often the customers are looked at. A handover window is measured in
    /// in-game hours, so a couple of seconds is far finer than it needs to be,
    /// and the scan costs a few interop calls per customer.
    /// </summary>
    /// <remarks>
    /// <b>And it is not what completes a handover under <i>when I talk to
    /// them</i>.</b> The owner's own session says why: at 20:39:05Z the sweep had
    /// him inside the range the game reported and the game still answered
    /// <c>IsReadyForHandover(true)</c> false two seconds later; by the time it
    /// was true he had pressed <c>E</c>, and the next sweep was up to two seconds
    /// away. No smaller interval closes that gap — the dialogue opening does.
    /// See <see cref="WhenTheyAreTalkedTo"/>.
    /// </remarks>
    private const float ScanSeconds = 2f;

    private readonly Func<bool> _enabled;
    private readonly Func<AdvisorSettings> _settings;
    private readonly LifecycleWatch _lifecycle;
    private readonly Action<string> _log;
    private readonly Action<string> _warn;

    private readonly ContractClaimRegistry _claims = new();

    /// <summary>
    /// Keeps the scan from repeating itself. A refusal is worth one line, not
    /// one line every two seconds until the window closes.
    /// </summary>
    private readonly SaidOnce _said = new();

    /// <summary>
    /// The same, for everything that belongs to the scan rather than to a
    /// contract: the standing conditions the lifecycle gate reports — no save
    /// loaded, a save being written, this machine not being the host — and what
    /// goes wrong with the scan itself. Kept apart from <see cref="_said"/>
    /// because that one is forgotten along with the contracts, and these must
    /// not be: a scan that fails fails whether or not any contract is live, and
    /// a line repeated every two seconds for a whole session is how the lines
    /// that matter get buried.
    ///
    /// Each of those is a subject of its own, so a failing customer read and a
    /// failing scan do not overwrite each other's memory and set both warning
    /// again on the next pass.
    /// </summary>
    private readonly SaidOnce _saidAboutTheScan = new();

    private float _nextScan;

    /// <param name="lifecycle">
    /// The mod's one reading of whether it may act at all. Asked before every
    /// scan: a handover sends money and goods, and doing that into the middle of
    /// a save is the worst version of the bug this guard exists for.
    /// </param>
    public AutomaticHandover(
        Func<bool> enabled,
        Func<AdvisorSettings> settings,
        LifecycleWatch lifecycle,
        Action<string> log,
        Action<string> warn)
    {
        _enabled = enabled;
        _settings = settings;
        _lifecycle = lifecycle;
        _log = log;
        _warn = warn;
    }

    /// <summary>
    /// Look at every customer once a scan interval. Never throws into the
    /// game's update loop.
    /// </summary>
    /// <remarks>
    /// There used to be a <c>Verdict()</c> beside this, which asked the gate
    /// without acting on it so that the Automation tab's block header could
    /// print the answer shortened. There is no block header: <c>app.md</c>
    /// deletes every status word — <i>"Ich gehe davon aus, wenn ich es gesetzt
    /// habe, dann läuft es."</i> — and nothing called it. What the gate says now
    /// goes to the debug record and nowhere else.
    /// </remarks>
    public void Tick()
    {
        try
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextScan)
            {
                return;
            }

            _nextScan = now + ScanSeconds;

            // May this machine act at all right now? The switch, the save, the
            // load and the server, in one place. The handover gate states the
            // switch and the server again for every customer; this is so that a
            // client, or a host with the switch off, does no work at all — and
            // so that nothing is handed over while the game is writing a save.
            LifecycleVerdict verdict = LifecycleWatch.Ask(
                _lifecycle, LifecyclePass.Handover, _enabled());

            if (verdict.Outcome != LifecycleOutcome.Act)
            {
                if (verdict.WorthReporting)
                {
                    AboutTheScan(LifecyclePass.Handover, verdict.Reason);
                }

                return;
            }

            Scan();
        }
        catch (Exception error)
        {
            // Said once. A scan that fails for a standing reason fails again in
            // two seconds and every two seconds after that, and the host needs
            // to read it once, not for the rest of the evening.
            AboutTheScan(
                "scan",
                $"Dealcraft could not scan for handovers: {error.Message}",
                warn: true);
        }
    }

    /// <summary>
    /// The player has just opened the dialogue with this customer. Act on it
    /// now, not on the next sweep.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the whole of ticket 52.</b> The owner watched two handovers
    /// complete by themselves and the third fail because he beat the sweep by six
    /// seconds: he pressed <c>E</c>, nothing happened, and he finished the deal
    /// by hand — which is what takes Dealcraft off that contract for good
    /// (<see cref="NoteAnyOpenScreen"/> and
    /// <see cref="ContractClaimRegistry.NoteHumanInteraction"/>). Polling is what
    /// made the other two luck.
    /// </para>
    /// <para>
    /// <b>Called from a Harmony prefix</b> on
    /// <c>DialogueController.Interacted()</c> — see
    /// <see cref="HandoverDialoguePatches"/> — so it runs before the game has
    /// built the choice list the player will be shown. When it hands over,
    /// <c>Customer.ProcessHandover</c> lowers <c>IsAwaitingDelivery</c>
    /// (<c>+0x138</c>, written false) on the way through,
    /// so <c>GetActiveChoices()</c> a few instructions later does not offer
    /// <i>Complete Deal</i> at all and there is nothing left for the player to
    /// click.
    /// </para>
    /// <para>
    /// <b>It asks the lifecycle guard exactly as the sweep does.</b> A handover
    /// sends money and goods, and doing that into the middle of a save is the
    /// worst version of the bug that guard exists for — pressing <c>E</c> at the
    /// wrong moment must not be a way round it. What it does not do is
    /// reconcile: that is the sweep's periodic housekeeping and it runs two
    /// seconds either side of this.
    /// </para>
    /// </remarks>
    public void WhenTheyAreTalkedTo(Customer customer)
    {
        try
        {
            if (customer == null)
            {
                return;
            }

            LifecycleVerdict verdict = LifecycleWatch.Ask(
                _lifecycle, LifecyclePass.Handover, _enabled());

            if (verdict.Outcome != LifecycleOutcome.Act)
            {
                if (verdict.WorthReporting)
                {
                    AboutTheScan(LifecyclePass.Handover, verdict.Reason);
                }

                return;
            }

            Consider(customer, _settings(), _enabled(), theyOpenedTheDialogue: true);
        }
        catch (Exception error)
        {
            // Never into the game's interaction handling. A dialogue that fails
            // to open because a mod threw is a broken game, not a missed deal.
            AboutTheScan(
                "dialogue",
                $"Dealcraft broke off a handover on the dialogue opening: {error.Message}",
                warn: true);
        }
    }

    private void Scan()
    {
        Reconcile();
        NoteAnyOpenScreen();

        Il2CppSystem.Collections.Generic.List<Customer> customers = Customer.UnlockedCustomers;
        if (customers is null)
        {
            return;
        }

        // Read once for the whole pass, and passed to the gate rather than
        // assumed: the gate is where the rule lives, so it gets the real values
        // even though Tick has already looked at them. The server is no longer
        // among them — a handover belongs to whoever is carrying the goods.
        AdvisorSettings settings = _settings();
        bool enabled = _enabled();

        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];
            if (customer == null)
            {
                continue;
            }

            try
            {
                Consider(customer, settings, enabled, theyOpenedTheDialogue: false);
            }
            catch (Exception error)
            {
                // One customer despawning mid-read must not stop the others —
                // and must not fill the log either. Said once per reason rather
                // than once per customer per scan: a customer who cannot be read
                // is usually one the whole session cannot read, so this is worth
                // one line and then silence until the reason changes.
                AboutTheScan(
                    "customer",
                    $"Dealcraft broke off reading a customer: {error.Message}",
                    warn: true);
            }
        }
    }

    /// <summary>
    /// A contract whose handover screen a player has opened belongs to that
    /// player from then on. The registry locks it for the rest of its life;
    /// closing the screen again does not hand it back.
    /// </summary>
    private void NoteAnyOpenScreen()
    {
        if (!Singleton<HandoverScreen>.InstanceExists)
        {
            return;
        }

        HandoverScreen screen = Singleton<HandoverScreen>.Instance;
        if (screen == null || !screen.IsOpen)
        {
            return;
        }

        Contract opened = screen.CurrentContract;
        if (opened == null)
        {
            return;
        }

        string key = KeyOf(opened);
        _claims.NoteHumanInteraction(key);
        Say(key, "A player opened this contract's handover screen; Dealcraft is leaving it to them.");
    }

    /// <summary>
    /// Forget contracts the game no longer has, so the claim registry and the
    /// log memory stay in step with a save that was reloaded.
    /// </summary>
    private void Reconcile()
    {
        var live = new List<string>();
        Il2CppSystem.Collections.Generic.List<Contract> contracts = Contract.Contracts;

        if (contracts is not null)
        {
            for (int i = 0; i < contracts.Count; i++)
            {
                Contract contract = contracts[i];
                if (contract != null)
                {
                    live.Add(KeyOf(contract));
                }
            }
        }

        _claims.Reconcile(live);
        _said.Forget(live);
    }

    /// <param name="theyOpenedTheDialogue">
    /// Whether this call was caused by the player opening the dialogue with this
    /// customer. The sweep passes false; <see cref="WhenTheyAreTalkedTo"/> passes
    /// true. Under <i>when I talk to them</i> it is what the gate lets through
    /// on, and it is an event rather than a state — see
    /// <see cref="HandoverSituation.TheyOpenedTheDialogue"/>.
    /// </param>
    private void Consider(
        Customer customer, AdvisorSettings settings, bool enabled, bool theyOpenedTheDialogue)
    {
        Contract contract = customer.CurrentContract;
        if (contract == null)
        {
            return;
        }

        string key = KeyOf(contract);
        NPC npc = customer.NPC;
        string name = npc == null ? string.Empty : npc.FullName ?? string.Empty;

        bool valid = customer.IsHandoverChoiceValid(out string invalidReason);

        var situation = new HandoverSituation(
            CustomerName: name,
            ContractKey: key,
            // This customer's own network object, not the session's: the vanilla
            // handover call writes through it. Read per customer because a
            // session-wide client half being up does not make this NPC's ready.
            CanReachTheServer: customer.IsClientInitialized,
            AutomationEnabled: enabled,
            // true is the dialogue choice's own Enabled flag, which
            // Customer.SetUpDialogue raises once and never lowers. The game
            // passes exactly this when it decides whether to draw Complete Deal.
            IsReadyForHandover: customer.IsReadyForHandover(true),
            IsHandoverChoiceValid: valid,
            HandoverChoiceInvalidReason: invalidReason ?? string.Empty,

            // The player's answer to when a handover may happen, read afresh
            // with the rest of the settings so that a change in the app applies
            // to the next handover rather than to the next session.
            Place: settings.Place,
            TheyOpenedTheDialogue: theyOpenedTheDialogue);

        HandoverDecision decision = HandoverGate.Decide(situation);

        switch (decision.Action)
        {
            case HandoverAction.HandOver:
                Attempt(customer, contract, key, name, settings.Reach, settings.Place);
                return;

            case HandoverAction.Refuse:
                Say(key, decision.Reason);
                Record(decision.Action, name, key, contract, decision.Reason);
                return;

            default:
                // Waiting and abstaining are the ordinary states of a
                // scheduled deal. Saying so every two seconds would bury the
                // lines that matter — in the log. The record is the other way
                // round: "nothing happened and here is why" is the question a
                // player cannot answer from inside the game, and the record
                // writes a reason once and repeats it only when it changes, so
                // a whole delivery window of waiting costs one line.
                Record(decision.Action, name, key, contract, decision.Reason);
                return;
        }
    }

    /// <param name="place">
    /// Which of the two answers let this through. Recorded with the handover so
    /// that a session can be read back without knowing what the preferences file
    /// held at the time — which is the whole reason the record exists.
    /// </param>
    private void Attempt(
        Customer customer,
        Contract contract,
        string key,
        string name,
        GradeReach reach,
        HandoverPlace place)
    {
        if (!Screens(out string missing))
        {
            Say(key, $"Dealcraft will not hand over to {Named(name)}: {missing}.");
            Record(HandoverAction.Refuse, name, key, contract, missing);
            return;
        }

        DeliveryPlan delivery = Plan(customer, contract, reach);
        if (!delivery.CanDeliver)
        {
            Say(key, $"Dealcraft is not handing over to {Named(name)} yet: {delivery.Why}");

            // One of the silences: nothing of the product in reach, not enough
            // carried, too many packages for one handover, nothing at or above
            // the grade asked for, or only a better grade covers it while the
            // player asked for exactly the grade that was ordered. Each arrives
            // here as the sentence its own search wrote, and it is copied rather
            // than re-worded — the last of them is the only account there is of
            // a delivery the setting refused, because the game shows nothing.
            Record(HandoverAction.Refuse, name, key, contract, delivery.Why);
            return;
        }

        // Claimed last, and only once the handover is certain: a claim is never
        // given back while the contract lives, so claiming a contract we then
        // decline would lock the automation out of it for good.
        ClaimDecision claim = _claims.Claim(key);
        if (claim.Outcome != ClaimOutcome.Claimed)
        {
            Say(key, $"Dealcraft stood down on {Named(name)}'s contract: {claim.Reason}.");
            Record(HandoverAction.Abstain, name, key, contract, claim.Reason);
            return;
        }

        var items = new Il2CppSystem.Collections.Generic.List<ItemInstance>();

        // What actually came out of the bag, per line, kept so that the record
        // reports the handover that happened rather than the one that was
        // planned. Packages cannot be split, so these two differ often enough
        // to matter: the plan asks for units and the pockets answer in jars.
        var handed = new List<DeliveredGrade>(delivery.Plans.Count);

        try
        {
            foreach (GradePlan plan in delivery.Plans)
            {
                Report(plan, name);

                Il2CppSystem.Collections.Generic.List<ItemInstance> taken = HandoverGoods.Take(
                    plan.Request.ProductId, plan.ChosenQuality, plan.ChosenUnits, out int unitsTaken);

                for (int i = 0; i < taken.Count; i++)
                {
                    items.Add(taken[i]);
                }

                handed.Add(new DeliveredGrade(
                    plan.Request.ProductId, plan.ChosenQuality, taken.Count, unitsTaken));

                if (unitsTaken < plan.ChosenUnits)
                {
                    // The plan said this grade covered the order and the
                    // inventory then produced less. Short-changing a customer
                    // is worse than missing the window, so nothing is sent.
                    throw new InvalidOperationException(
                        $"only {unitsTaken} of {plan.ChosenUnits} units of {plan.Request.ProductId} "
                            + "could be taken out of the inventory");
                }

                if (unitsTaken > plan.ChosenUnits)
                {
                    // Product travels in packages, and the delivery is already
                    // the least combination that covers the order — so this is
                    // what the pockets forced, not a choice. Said out loud
                    // because the player can carry something else next time.
                    _log($"[{Named(name)}] {unitsTaken} units for a contract of {plan.ChosenUnits}: "
                        + "nothing smaller in your bag.");
                }
            }

            // The vanilla call. It scores the delivery, builds the bonuses,
            // pays the player and sends ProcessHandoverServerSide, which is
            // what every other player sees the result of.
            customer.ProcessHandover(
                HandoverScreen.EHandoverOutcome.Finalize,
                contract,
                items,
                handoverByPlayer: true,
                giveBonuses: true);

            _log($"Dealcraft completed the handover to {Named(name)}.");

            // One row per line of the contract, because a contract with two
            // products was handed over as two grades and the record that only
            // named one would be the record that could not answer which.
            foreach (DeliveredGrade line in handed)
            {
                Record(
                    HandoverAction.HandOver,
                    name,
                    key,
                    contract,
                    $"handed over {line.Units} units of {line.ProductId} as "
                        + $"{QualityTier.Name(line.Grade)} in {line.Packages} package(s), "
                        + Under(place),
                    acted: true,
                    line);
            }
        }
        catch (Exception error)
        {
            // The goods left the host's inventory before the call. Whatever
            // went wrong, they go back rather than vanish.
            HandoverGoods.Return(items);
            _warn($"Dealcraft could not complete the handover to {Named(name)} ({error.Message}). "
                + "The goods were put back, and this contract is now left to you: "
                + "the automation does not retry a handover that went wrong.");

            // The most expensive nothing this mod can produce: everything was
            // in order, the goods left the bag, and the game refused anyway.
            Record(
                HandoverAction.Refuse,
                name,
                key,
                contract,
                $"everything was in order and the handover itself failed ({error.Message}); "
                    + "the goods were put back and the contract is left to the player");
        }
        finally
        {
            // Released whether it worked or not. The registry does not hand a
            // live contract back, which is the point: one attempt per contract,
            // and a failed one is a job for a player rather than for a loop.
            _claims.Release(key);
        }
    }

    /// <summary>
    /// One line of the debug record: what this contract's handover came to, and
    /// why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written for waiting and abstaining as well as for refusing and handing
    /// over, which is the opposite of what the log does. The log is read while
    /// something is going wrong and a line every two seconds buries the ones
    /// that matter; the record is read afterwards, holds one row per reason
    /// until the reason changes, and exists precisely for the cases where
    /// nothing happened. Those are the ones a player cannot see from inside the
    /// game.
    /// </para>
    /// <para>
    /// The contract's payment goes in rather than what the handover paid: this
    /// runs before the game's call, and what it paid is read off the game's own
    /// RPC into <c>handover-ledger.jsonl</c>. Two files, one contract key, and
    /// the two halves of the same handover.
    /// </para>
    /// </remarks>
    private static void Record(
        HandoverAction action,
        string name,
        string key,
        Contract contract,
        string reason,
        bool acted = false,
        DeliveredGrade? handed = null) =>
        DecisionRecord.Handover(
            action,
            acted,
            reason,
            name,
            key,
            handed?.ProductId,
            handed?.Grade,
            handed?.Packages,
            handed?.Units,
            PaymentOf(contract));

    /// <summary>
    /// Which of the two answers this handover went out under, for the record.
    /// </summary>
    private static string Under(HandoverPlace place) =>
        place == HandoverPlace.FromAnywhere ? "from anywhere" : "when I talk to them";

    /// <summary>
    /// <c>Contract.Payment</c>, or nothing if the contract cannot be read. A
    /// record that quietly wrote a zero would be indistinguishable from a
    /// contract that really pays nothing.
    /// </summary>
    private static float? PaymentOf(Contract contract)
    {
        try
        {
            return contract == null ? null : contract.Payment;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Plan every line of the contract. All of them have to be deliverable, or
    /// none of them is: a half-filled order is worse than a late one.
    /// </summary>
    private static DeliveryPlan Plan(Customer customer, Contract contract, GradeReach reach)
    {
        ProductList products = contract.ProductList;
        Il2CppSystem.Collections.Generic.List<ProductList.Entry> entries = products?.entries;

        if (entries is null || entries.Count == 0)
        {
            return DeliveryPlan.Nothing("the contract names no products");
        }

        CustomerData data = customer.CustomerData;
        if (data == null)
        {
            return DeliveryPlan.Nothing("the game holds no customer data for them");
        }

        // The customer's standards are an ECustomerStandard; the game maps them
        // onto the quality ladder itself rather than us assuming the ladders
        // line up.
        int standard = (int)StandardsMethod.GetCorrespondingQuality(data.Standards);
        float payment = contract.Payment;
        var plans = new List<GradePlan>(entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            ProductList.Entry entry = entries[i];
            if (entry is null)
            {
                return DeliveryPlan.Nothing("the contract has a line the game could not read");
            }

            var request = new DeliveryRequest(
                ProductId: entry.ProductID ?? string.Empty,
                RequestedQuality: (int)entry.Quality,
                RequestedQuantity: entry.Quantity,
                ContractPayment: payment,
                CustomerStandard: standard);

            // The grade and the packages in one question. Asked separately it
            // cost contracts: a grade with the units for the order and too many
            // packages to put on the screen was chosen and then refused, while
            // a larger package of a better grade that covered it sat in the same
            // pocket. The plan that comes back is one the bag can hand over.
            //
            // How far above the contract's grade it may look is the player's,
            // and it is read afresh for every scan with the rest of the
            // settings: the answer changed in the app applies to the next
            // handover, not to the next session.
            GradePlan plan = GradeChoice.Plan(
                request, HandoverGoods.LotsInReach(request.ProductId), reach, out PackageFill _);

            if (!plan.CanDeliver)
            {
                return DeliveryPlan.Nothing(plan.Reason);
            }

            plans.Add(plan);
        }

        return DeliveryPlan.Deliver(plans);
    }

    /// <summary>
    /// Say what is being handed over and what the grades left behind would have
    /// been worth. This is the part a player reads the morning after.
    /// </summary>
    private void Report(GradePlan plan, string name)
    {
        foreach (string line in GradeChoice.Describe(plan, GameMoney.Rounded))
        {
            _log($"[{Named(name)}] {line}");
        }
    }

    /// <summary>
    /// <c>Customer.ProcessHandover</c> reaches for the handover screen and the
    /// completion popup on the way through, and throws if either is missing.
    /// Better to find that out before the goods have left the host's hands.
    /// </summary>
    private static bool Screens(out string missing)
    {
        if (!Singleton<HandoverScreen>.InstanceExists)
        {
            missing = "the game's handover screen is not in this scene";
            return false;
        }

        if (!Singleton<DealCompletionPopup>.InstanceExists)
        {
            missing = "the game's deal completion popup is not in this scene";
            return false;
        }

        missing = string.Empty;
        return true;
    }

    private void Say(string contractKey, string message)
    {
        if (_said.ShouldSay(contractKey, message))
        {
            _log(message);
        }
    }

    /// <summary>
    /// Say something about the scan itself rather than about a contract: why it
    /// is standing down, or why it failed. Once per subject, and again when what
    /// there is to say about that subject changes.
    /// </summary>
    private void AboutTheScan(string subject, string message, bool warn = false)
    {
        if (!_saidAboutTheScan.ShouldSay(subject, message))
        {
            return;
        }

        if (warn)
        {
            _warn(message);
        }
        else
        {
            _log(message);
        }
    }

    /// <summary>
    /// A contract's own GUID, which is the same on every machine and lives
    /// exactly as long as the contract does.
    /// </summary>
    private static string KeyOf(Contract contract)
    {
        try
        {
            return contract.GUID.ToString();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string Named(string name) => string.IsNullOrWhiteSpace(name) ? "the customer" : name.Trim();
}
