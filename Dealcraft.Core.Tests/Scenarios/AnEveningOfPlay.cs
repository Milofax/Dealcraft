using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Dealcraft.Core.Tests.Scenarios;

/// <summary>
/// What every scenario needs before it can be played: a screen, a clock's worth
/// of windows, and a way to write down what the customer's own curve is.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here is a plain value the mod's readers already hand inward.</b>
/// The counteroffer screen is four numbers (<see cref="CounterofferLimits"/>);
/// the customer is <c>Customer.GetOfferSuccessChance</c>, which is a function
/// from a deal to a probability; the bag is a list of
/// <see cref="CarriedLot"/>; the windows the game is offering are a list of
/// <see cref="DealWindow"/>. Nothing in this file stands in for a decision — the
/// decisions are the mod's and are called, not copied.
/// </para>
/// </remarks>
internal static class AnEveningOfPlay
{
    /// <summary>
    /// The counteroffer screen, as the scenarios use it.
    /// </summary>
    /// <remarks>
    /// <b>These four numbers are the one input stage 1b cannot read.</b> The mod
    /// takes them off the live screen — <c>CounterofferInterface.MinQuantity</c>,
    /// <c>screen.MaxQuantity</c> and the price selector's ends — and they are
    /// Unity inspector fields on the prefab, not constants in
    /// <c>Assembly-CSharp.dll</c>, so can see the
    /// fields and not their values. A screen roomier than any deal here is
    /// therefore assumed, which is the assumption that keeps the screen out of
    /// every scenario's answer: nothing below is ever clamped by it. Stage 2
    /// reads the real four off a running game.
    /// </remarks>
    public static CounterofferLimits TheCounterofferScreen { get; } =
        new(minQuantity: 1, maxQuantity: 20, minPrice: 1f, maxPrice: 10_000f);

    /// <summary>The windows the player has ticked.</summary>
    public static DealWindowSet Allowing(params DealWindow[] windows)
    {
        var set = new DealWindowSet();
        foreach (DealWindow window in windows)
        {
            set.Allow(window, allow: true);
        }

        return set;
    }

    /// <summary>
    /// A customer's own acceptance curve, written down as the scenario states
    /// it: the offer they made is certain, and the chance falls away above it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the value <c>Customer.GetOfferSuccessChance</c> returns and
    /// nothing more. It is monotone in price, which is what
    /// <see cref="PriceCurveSearch"/> bisects against; the game's own figure is
    /// too, and the project notes establishes that it rolls no
    /// dice.
    /// </para>
    /// <para>
    /// <b>A scenario that names one point on a curve has not named the curve.</b>
    /// Two customers can both say yes to the same price at the same chance and
    /// still be worth different counters, because
    /// <see cref="ConfidenceSearch.Best"/> ranks on <c>chance × price</c> over
    /// the whole curve. That is why this takes a shape and not a point, and it
    /// is the finding behind scenario 1.
    /// </para>
    /// </remarks>
    /// <param name="certainUpTo">The total the customer is already offering.</param>
    /// <param name="anchorTotal">A total the scenario names a chance for.</param>
    /// <param name="anchorChance">The chance the scenario names for it.</param>
    /// <param name="zeroAt">
    /// Where the chance reaches nothing. Above the anchor, and the closer to it
    /// the steeper the cliff.
    /// </param>
    public static Func<int, float, float> ACustomerWhoFallsAway(
        float certainUpTo, float anchorTotal, float anchorChance, float zeroAt)
    {
        if (!(anchorTotal > certainUpTo) || !(zeroAt > anchorTotal))
        {
            throw new ArgumentException(
                "the curve runs certain, then the anchor, then nothing", nameof(anchorTotal));
        }

        return (_, total) =>
        {
            if (total <= certainUpTo)
            {
                return 1f;
            }

            if (total <= anchorTotal)
            {
                return 1f - ((1f - anchorChance) * (total - certainUpTo) / (anchorTotal - certainUpTo));
            }

            float chance = anchorChance - (anchorChance * (total - anchorTotal) / (zeroAt - anchorTotal));
            return chance < 0f ? 0f : chance;
        };
    }
}

/// <summary>
/// The Automation tab, built from the settings a scenario names.
/// </summary>
/// <remarks>
/// Built through <see cref="AutomationForm.Build"/> off the three catalogues the
/// adapter fills from MelonPreferences, so what a scenario is held against is
/// the page the player would see and not a description of it.
/// </remarks>
internal static class AFullyConfiguredPage
{
    public static AutomationForm Of(
        AdvisorSettings settings,
        DealWindowSet windows,
        bool maintainingPrices = false,
        ServerAuthority authority = ServerAuthority.Held,
        IReadOnlyList<DealWindowHours>? hours = null)
    {
        var entries = new List<AutomationSetting>();
        entries.AddRange(AutomationCatalog.Describe(settings));
        entries.AddRange(DealWindowCatalog.Describe(
            windows, hours ?? Array.Empty<DealWindowHours>()));
        entries.AddRange(PriceMaintenanceCatalog.Describe(maintainingPrices));

        return AutomationForm.Build(entries, authority);
    }
}

/// <summary>
/// The set of scenarios cannot rot: every one of 1 to 7 has a driver, every
/// driver names a scenario that exists, and every driver names it by the heading
/// the file currently gives it.
/// </summary>
/// <remarks>
/// Ticket 29 asks for exactly this — <i>"A scenario with no driver is itself
/// reported, so the set cannot rot"</i> — and it is the one test here that fails
/// when nobody has done anything, which is the point of it.
/// </remarks>
public class TheSetOfScenariosTests
{
    /// <summary>
    /// Scenario 8 is the owner's, played with real players. Stage 1b cannot
    /// produce its one condition — two machines, two players, one shared roster
    /// — so it has no driver and must not be given a green one.
    /// </summary>
    private const int PlayedWithFriends = 8;

    [Fact]
    public void Every_scenario_from_one_to_seven_has_a_driver()
    {
        IReadOnlyDictionary<int, DrivesAttribute> drivers = Drivers();

        int[] undriven = TheScenarioFile.All.Keys
            .Where(number => number != PlayedWithFriends && !drivers.ContainsKey(number))
            .OrderBy(number => number)
            .ToArray();

        Assert.True(
            undriven.Length == 0,
            $"{TheScenarioFile.Path} holds scenario(s) {string.Join(", ", undriven)} that nothing "
            + "drives. A scenario with no driver is reported rather than quietly skipped.");
    }

    [Fact]
    public void No_driver_names_a_scenario_the_file_does_not_hold()
    {
        int[] invented = Drivers().Keys
            .Where(number => !TheScenarioFile.All.ContainsKey(number))
            .OrderBy(number => number)
            .ToArray();

        Assert.Empty(invented);
    }

    /// <summary>
    /// And each driver names its scenario by the heading the file gives it, so a
    /// renamed scenario is one change to the file and one to the driver.
    /// </summary>
    [Fact]
    public void Every_driver_names_its_scenario_by_the_heading_the_file_gives_it()
    {
        foreach ((int number, DrivesAttribute driver) in Drivers().OrderBy(entry => entry.Key))
        {
            Assert.Equal(TheScenarioFile.Heading(number), driver.Heading);
        }
    }

    /// <summary>
    /// Scenario 8 is not claimed from here. `proof.md` puts it in stage 3 and
    /// `scenarios.md` says so itself; a driver appearing for it would be the
    /// claim this test exists to stop.
    /// </summary>
    [Fact]
    public void Scenario_eight_is_played_with_friends_and_is_not_driven_here()
    {
        Assert.Contains(PlayedWithFriends, TheScenarioFile.All.Keys);
        Assert.DoesNotContain(PlayedWithFriends, Drivers().Keys);

        TheScenarioFile.Says(
            PlayedWithFriends,
            "**This one is tested by the owner with real players.** Not claimed as passing");
    }

    /// <summary>
    /// The quotation guard can be made to fail, which is the only reason to
    /// trust it. Every driver's assertions go through
    /// <see cref="TheScenarioFile.Says"/>; a guard that passed whatever it was
    /// handed would make "the scenario's own words" a claim rather than a check.
    /// </summary>
    [Fact]
    public void A_phrase_the_scenario_does_not_say_is_refused()
    {
        // Really in scenario 1, and wrapped across two lines in the file.
        TheScenarioFile.Says(1, "Beth Penn texts: she wants **8 OG Kush for $640**");

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => TheScenarioFile.Says(1, "Beth Penn texts: she wants 9 OG Kush"));

        Assert.Contains("no longer says", refused.Message, StringComparison.Ordinal);

        // And a phrase from one scenario is not accepted as another's.
        Assert.Throws<InvalidOperationException>(
            () => TheScenarioFile.Says(7, "Beth Penn texts"));
    }

    private static IReadOnlyDictionary<int, DrivesAttribute> Drivers()
    {
        var drivers = new Dictionary<int, DrivesAttribute>();

        foreach (Type type in typeof(TheSetOfScenariosTests).Assembly.GetTypes())
        {
            if (type.GetCustomAttribute<DrivesAttribute>() is DrivesAttribute driver)
            {
                drivers[driver.Scenario] = driver;
            }
        }

        return drivers;
    }
}
