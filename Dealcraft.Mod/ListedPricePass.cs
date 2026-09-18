using System;
using Dealcraft.Core;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;
using UnityEngine;

namespace Dealcraft;

/// <summary>
/// Keeps every listed product's price where the player's customers still clear
/// it, while the player has asked for that. Host only, and off on a fresh
/// install.
/// </summary>
/// <remarks>
/// <para>
/// One decision for the whole book, which is what the owner asked for —
/// <i>überall</i>, not a button per product. The pass walks
/// <c>ProductManager.ListedProducts</c>, asks
/// <see cref="ProductValuationReader"/> what each is worth to the customers who
/// could order it, and writes the price
/// <see cref="PricingRecommendation.Best"/> names through
/// <see cref="ListedPriceWriter"/>.
/// </para>
/// <para>
/// <b>It is never silent.</b> A price write changes every future offer from
/// every customer, including the ones a dealer handles, so every product that
/// moves gets a line saying what it moved from and to, and every product left
/// alone says why.
/// </para>
/// <para>
/// <b>Turning it off writes nothing and restores nothing.</b> Prices stay where
/// the last pass put them. Restoring is a second behaviour with a store of its
/// own and the owner did not ask for one; the switch's own line says so.
/// </para>
/// <para>
/// Priced in whole passes rather than per frame: a reading probes customers, so
/// the pass runs on the same cadence as a customer roster changes rather than
/// every tick. A product whose price is already right costs a reading and no
/// write.
/// </para>
/// </remarks>
internal sealed class ListedPricePass
{
    /// <summary>
    /// How long between passes. The listed price only needs to move when the
    /// customer roster or their budgets have, which happens on the scale of game
    /// days, so a minute of real time is already generous and keeps the probing
    /// off the frame budget.
    /// </summary>
    private const float PassIntervalSeconds = 60f;

    /// <summary>
    /// Most products one pass reads. Each reading probes up to thirty-two
    /// customers, so an unbounded book would put an unbounded cost in one frame.
    /// The rest are taken by the next pass; the cursor is kept.
    /// </summary>
    private const int ProductsPerPass = 4;

    private readonly Func<PriceMaintenanceConfiguration> _configuration;
    private readonly LifecycleWatch _lifecycle;
    private readonly Action<string> _log;
    private readonly Action<string> _warn;
    private readonly SaidOnce _said = new();

    private int _cursor;
    private float _nextPass;

    public ListedPricePass(
        Func<PriceMaintenanceConfiguration> configuration,
        LifecycleWatch lifecycle,
        Action<string> log,
        Action<string> warn)
    {
        _configuration = configuration;
        _lifecycle = lifecycle;
        _log = log;
        _warn = warn;
    }

    public void SceneChanged()
    {
        _said.Forget(Array.Empty<string>());
        _cursor = 0;
        _nextPass = 0f;
    }

    public void Tick()
    {
        try
        {
            Pass();
        }
        catch (Exception error)
        {
            // Never throw into the game's update loop. A pass that failed
            // halfway leaves the prices it already wrote, which is the same
            // state a player who typed half of them by hand would be in.
            _cursor = 0;
            _nextPass = Time.realtimeSinceStartup + PassIntervalSeconds;
            _warn($"the price pass failed and was abandoned ({error.Message})");
        }
    }

    private void Pass()
    {
        PriceMaintenanceConfiguration configuration = _configuration();

        LifecycleVerdict verdict = LifecycleWatch.Ask(
            _lifecycle, LifecyclePass.Prices, configuration.Maintaining);

        if (verdict.Outcome != LifecycleOutcome.Act)
        {
            if (verdict.Outcome == LifecycleOutcome.StandDown)
            {
                _cursor = 0;
            }

            if (verdict.WorthReporting)
            {
                Say(LifecyclePass.Prices, verdict.Reason);
            }

            return;
        }

        if (_cursor == 0 && Time.realtimeSinceStartup < _nextPass)
        {
            return;
        }

        SayTheLimits();

        Il2CppSystem.Collections.Generic.List<ProductDefinition> listed = ProductManager.ListedProducts;
        if (listed is null || listed.Count == 0)
        {
            EndPass();
            return;
        }

        int read = 0;
        while (_cursor < listed.Count && read < ProductsPerPass)
        {
            ProductDefinition product = listed[_cursor];
            _cursor++;

            if (product == null)
            {
                continue;
            }

            read++;
            Consider(product, configuration.Settings);
        }

        if (_cursor >= listed.Count)
        {
            EndPass();
        }
    }

    private void Consider(ProductDefinition product, AdvisorSettings settings)
    {
        ProductValuation valuation = ProductValuationReader.Read(product, settings);

        if (!valuation.Available)
        {
            Say($"read:{valuation.ProductId}", $"{valuation.ProductName}: {valuation.UnavailableReason}");
            DecisionRecord.ListedPrice(
                ListedPriceOutcome.CannotCompute,
                acted: false,
                $"{valuation.ProductName}: {valuation.UnavailableReason}",
                valuation.ProductId);
            return;
        }

        ListedPriceDecision decision = ListedPriceDecision.For(
            maintaining: true,
            valuation.ProductName,
            valuation.AskingPrice,
            PricingRecommendation.Best(valuation.Candidates));

        // Whatever the reading had to step over rides with the decision. The
        // joining is the valuation's own, so it is a test in Dealcraft.Core
        // rather than a line only a running game reaches.
        string reason = valuation.Explaining(decision.Reason);

        switch (decision.Outcome)
        {
            case ListedPriceOutcome.CannotCompute:

                // Said once per product: a product nobody can order stays that
                // way until the roster changes, and a line a pass repeats every
                // minute is a line nobody reads.
                Say($"nobody:{valuation.ProductId}", reason);
                Record(valuation, decision, reason);
                return;

            case ListedPriceOutcome.AlreadyThere:

                // Recorded although nothing happened, and this is the point of
                // the file: a price the mod is holding where it already is
                // looks exactly like a mod that is not running.
                Record(valuation, decision, reason);
                return;

            case ListedPriceOutcome.Write:
                Write(valuation, decision, reason);
                return;
        }
    }

    private void Write(ProductValuation valuation, in ListedPriceDecision decision, string reason)
    {
        string refused = ListedPriceWriter.Write(valuation.ProductId, decision.Price);

        if (refused.Length > 0)
        {
            Say($"refused:{valuation.ProductId}", $"{reason} — except that {refused}");
            // The gate's verdict was Write; the game refused it. The outcome
            // keeps the gate's word and "acted" keeps the game's, which is the
            // only way the two can be told apart a session later.
            DecisionRecord.ListedPrice(
                decision.Outcome,
                acted: false,
                $"{reason} — except that {refused}",
                valuation.ProductId,
                valuation.AskingPrice,
                decision.Price);
            return;
        }

        // Always, and never through SaidOnce: a price that moved is news every
        // time it moves, and this is the line that makes the switch accountable.
        _log(reason);
        Record(valuation, decision, reason);
    }

    /// <summary>
    /// One line of the debug record: what this product's price came to, and
    /// why.
    /// </summary>
    /// <remarks>
    /// This is the pass with the most to answer for. <c>app.md</c> states the
    /// cost of deleting the <c>Products</c> tab plainly: <c>LIST IT AT</c> was
    /// the only place a listed price that silently costs customers was visible,
    /// overpricing produces no refusals but silence, and with the tab gone the
    /// price is visible nowhere in the game. So the record carries both sides
    /// of every move and the sentence that argued for it.
    /// </remarks>
    private static void Record(ProductValuation valuation, in ListedPriceDecision decision, string reason) =>
        DecisionRecord.ListedPrice(
            decision.Outcome,
            acted: decision.Outcome == ListedPriceOutcome.Write,
            reason,
            valuation.ProductId,
            valuation.AskingPrice,
            decision.Outcome == ListedPriceOutcome.Write ? decision.Price : null);

    /// <summary>
    /// The three figures every reading in this pass is built on, said once a
    /// session because they are the ones that were argued about.
    /// </summary>
    /// <remarks>
    /// Ticket 51 turned on what <c>ProductManager.MAX_PRICE</c> reads at
    /// runtime, and it could not be answered from the owner's four sessions
    /// because nothing ever printed it. Read against the binary it is a metadata
    /// literal <c>999</c> and the Il2CppInterop read of it is sound —
    /// <c>il2cpp_field_static_get_value</c> takes a literal out of metadata
    /// rather than out of static storage — but "read against the binary" is not
    /// the same as "seen in a run", and <c>CLAUDE.md</c> is clear about which of
    /// those two votes. This is the line that casts the other one.
    /// </remarks>
    private void SayTheLimits() =>
        Say(
            "limits",
            $"the game lists prices between {ProductManager.MIN_PRICE} and " +
            $"{ProductManager.MAX_PRICE} a unit, and puts at most " +
            $"{Customer.MaxOrderQuantityPerProduct} units of one product in an order.");

    private void EndPass()
    {
        _cursor = 0;
        _nextPass = Time.realtimeSinceStartup + PassIntervalSeconds;
    }

    /// <summary>
    /// One line per subject, and another only when the words change. A pass runs
    /// every minute for a whole session; a condition that has not moved is not
    /// news a second time.
    /// </summary>
    private void Say(string subject, string line)
    {
        if (_said.ShouldSay(subject, line))
        {
            _log(line);
        }
    }
}

/// <summary>
/// What one price pass needs, read fresh each pass so a hand-edited preferences
/// file takes effect without a restart.
/// </summary>
internal readonly struct PriceMaintenanceConfiguration
{
    public PriceMaintenanceConfiguration(bool maintaining, AdvisorSettings settings)
    {
        Maintaining = maintaining;
        Settings = settings;
    }

    /// <summary>The player's answer to the one question in the LISTED PRICE block.</summary>
    public bool Maintaining { get; }

    /// <summary>
    /// The host's own, so the ceilings this pass finds cost no more price points
    /// than they allow and are found at the confidence they asked for.
    /// </summary>
    public AdvisorSettings Settings { get; }
}
