using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The probe the search is allowed to use. `Customer.EvaluateCounteroffer`
/// folds a `UnityEngine.Random.Range` roll into its returned bool — read off
/// the game, — so it answers differently to
/// the same question and a bisection over it converges on noise.
/// `GetOfferSuccessChance` calls no Random and returns the chance itself, so
/// the verdict is made here instead, by comparing it to the host's threshold.
/// </summary>
public class OfferAcceptanceTests
{
    [Fact]
    public void An_offer_whose_chance_clears_the_threshold_is_accepted()
    {
        Assert.True(OfferAcceptance.ClearsThreshold(successChance: 0.95f, threshold: 0.9f));
        Assert.False(OfferAcceptance.ClearsThreshold(successChance: 0.85f, threshold: 0.9f));
    }

    /// <summary>
    /// A chance sitting exactly on the threshold is good enough: the host asked
    /// for "at least this likely", not "likelier than this".
    /// </summary>
    [Fact]
    public void An_offer_exactly_on_the_threshold_is_accepted()
    {
        Assert.True(OfferAcceptance.ClearsThreshold(successChance: 0.9f, threshold: 0.9f));
    }

    [Fact]
    public void A_probe_asks_the_game_for_the_chance_at_the_price_it_was_given()
    {
        (int Quantity, float Price) asked = default;

        OfferProbe probe = OfferAcceptance.Probe(
            (quantity, totalPrice) =>
            {
                asked = (quantity, totalPrice);
                return 1f;
            },
            threshold: 0.9f);

        Assert.True(probe(quantity: 4, totalPrice: 200f));
        Assert.Equal((4, 200f), asked);
    }

    /// <summary>
    /// The point of the whole change: the boundary the search converges on is
    /// now the price where the game's own chance falls through the host's
    /// threshold, and it is the same boundary every time it is searched.
    /// </summary>
    [Fact]
    public void The_search_settles_on_the_price_where_the_chance_falls_through_the_threshold()
    {
        // A customer whose chance decays a percentage point per pound over 100.
        float Chance(int quantity, float totalPrice) =>
            totalPrice <= 100f ? 1f : 1f - ((totalPrice - 100f) * 0.01f);

        var bounds = new OfferBounds(1, 1, 0f, 200f);

        PriceCurve first = PriceCurveSearch.Build(bounds, OfferAcceptance.Probe(Chance, threshold: 0.9f));
        PriceCurve again = PriceCurveSearch.Build(bounds, OfferAcceptance.Probe(Chance, threshold: 0.9f));

        // Chance hits exactly 0.9 at £110 and drops below it at £111.
        Assert.Equal(110f, Assert.Single(first.Points).TotalPrice);
        Assert.Equal(110f, Assert.Single(again.Points).TotalPrice);
    }

    /// <summary>
    /// A threshold of zero is the honest way to say "whatever they will take":
    /// every chance clears it, including a hopeless one.
    /// </summary>
    [Fact]
    public void A_threshold_of_zero_accepts_any_chance_at_all()
    {
        Assert.True(OfferAcceptance.ClearsThreshold(successChance: 0f, threshold: 0f));
    }
}
