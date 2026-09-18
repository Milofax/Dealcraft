using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// What the price search is allowed to ask the game.
///
/// `Customer.EvaluateCounteroffer` reads like exactly the predicate a bisection
/// wants — offer, verdict — and is the one method that must never be it. Read
/// off GameAssembly.dll (`docs/native-truth.md`), it folds a
/// `UnityEngine.Random.Range` roll into the bool it returns. The first test here
/// shows what that does to a search; the second stops an adapter reaching for it
/// again, which is not something a pure test can catch, because the call would
/// sit in the Il2Cpp adapter where no test may follow it.
/// </summary>
public class SearchProbeTests
{
    /// <summary>
    /// Stands in for `Random.Range(0, 0.4f)`: a run of values in its range that
    /// does not repeat within one search, so two searches over it see different
    /// answers to the same question exactly as two runs of the game would.
    /// </summary>
    private static readonly float[] Rolls = { 0.05f, 0.31f, 0.18f, 0.39f, 0.22f, 0.02f, 0.36f };

    /// <summary>
    /// The concrete harm. A rolled verdict makes the search answer a different
    /// price each time it is asked the same question, so nothing it returns is
    /// the boundary — it is wherever the dice fell on the way down.
    /// </summary>
    [Fact]
    public void A_bisection_over_a_rolled_verdict_answers_differently_each_time()
    {
        // A roll of our own rather than Random: the point is that something
        // other than the offer reaches the verdict, and a test that asserted
        // this through Random would be asserting Random's sequence, which no
        // runtime promises to keep across versions.
        int asked = 0;
        float Roll() => Rolls[asked++ % Rolls.Length];

        // The customer EvaluateCounteroffer describes: a real score, with the
        // roll added to it before the comparison.
        bool Rolled(int quantity, float totalPrice) =>
            (1f - (totalPrice / 400f)) + Roll() >= 0.5f;

        var bounds = new OfferBounds(1, 1, 0f, 400f);

        float first = Assert.Single(PriceCurveSearch.Build(bounds, Rolled).Points).TotalPrice;
        float again = Assert.Single(PriceCurveSearch.Build(bounds, Rolled).Points).TotalPrice;

        Assert.NotEqual(first, again);
    }

    /// <summary>
    /// The same search, probed the way the mod is required to probe it, lands on
    /// the same price both times. This is the whole of the difference.
    /// </summary>
    [Fact]
    public void A_bisection_over_the_success_chance_answers_the_same_every_time()
    {
        float Chance(int quantity, float totalPrice) => 1f - (totalPrice / 400f);

        var bounds = new OfferBounds(1, 1, 0f, 400f);
        OfferProbe probe = OfferAcceptance.Probe(Chance, threshold: 0.5f);

        float first = Assert.Single(PriceCurveSearch.Build(bounds, probe).Points).TotalPrice;
        float again = Assert.Single(PriceCurveSearch.Build(bounds, probe).Points).TotalPrice;

        Assert.Equal(200f, first);
        Assert.Equal(first, again);
    }

    /// <summary>
    /// Both readers that bisect — the advisor's curve and the product panel's
    /// per-customer ceiling — live in the Il2Cpp adapter, where a test cannot
    /// follow them. So the rule is checked where it can be: no adapter source
    /// calls the rolled verdict at all. Its name may still be written down, and
    /// is, in the comments saying why it is not called.
    /// </summary>
    [Fact]
    public void No_adapter_calls_the_rolled_verdict()
    {
        var offenders = new List<string>();

        foreach (string file in AdapterSource.Files())
        {
            int line = 0;

            foreach (string text in File.ReadLines(file))
            {
                line++;

                if (IsComment(text) || !text.Contains("EvaluateCounteroffer", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Doc comments are where this method's name belongs, so they do not count.
    /// </summary>
    private static bool IsComment(string text)
    {
        string trimmed = text.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal);
    }
}

