using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The identity a standing offer is claimed under. "Exactly once per contract"
/// is only as good as this string: two spellings of one offer are two contracts,
/// and one spelling of two offers is a customer who never gets answered.
///
/// Every case here is a lifecycle case. The day index is in the key, so the day
/// rolling over is the obvious way for the identity to move underneath a claim
/// that is already held.
/// </summary>
public class ContractKeyTests
{
    /// <summary>
    /// The key takes the moment the offer <em>arrived</em> and nothing else —
    /// there is no parameter for what time it is now. That is what makes the
    /// wrap survivable rather than merely survived: an offer that stands from
    /// half past eleven at night into the small hours is the same contract at
    /// both ends, because nothing about the key can have changed in between.
    /// </summary>
    [Fact]
    public void An_offer_that_stands_through_midnight_keeps_the_key_it_was_claimed_under()
    {
        var registry = new ContractClaimRegistry();

        // 23:30 on day four: the automation claims the offer and acts on it.
        string beforeMidnight = ContractKey.ForOffer("npc-jessi", elapsedDays: 4, timeOfDay: 2330);
        Assert.Equal(ClaimOutcome.Claimed, registry.Claim(beforeMidnight).Outcome);

        // 00:10 on day five, the same offer still on the table. The sweep reads
        // the same arrival off the customer and rebuilds the key from it.
        string afterMidnight = ContractKey.ForOffer("npc-jessi", elapsedDays: 4, timeOfDay: 2330);
        registry.Reconcile(new[] { afterMidnight });

        Assert.Equal(beforeMidnight, afterMidnight);
        Assert.Equal(ClaimOutcome.AlreadyClaimed, registry.Claim(afterMidnight).Outcome);
    }

    /// <summary>
    /// The other half of the wrap. Yesterday's half past eleven and today's half
    /// past eleven are two different offers from one customer, and the day index
    /// is the only thing that says so.
    /// </summary>
    [Fact]
    public void The_same_clock_time_on_two_days_is_two_contracts()
    {
        Assert.NotEqual(
            ContractKey.ForOffer("npc-jessi", elapsedDays: 4, timeOfDay: 2330),
            ContractKey.ForOffer("npc-jessi", elapsedDays: 5, timeOfDay: 2330));
    }

    /// <summary>
    /// The game's week is seven days but its day counter is not a weekday: it
    /// counts on. So the week rolling over is not a wrap at all, and an offer on
    /// the Monday after does not collide with one on the Monday before.
    /// </summary>
    [Fact]
    public void A_week_rolling_over_does_not_bring_a_key_back_round()
    {
        Assert.NotEqual(
            ContractKey.ForOffer("npc-jessi", elapsedDays: 6, timeOfDay: 1200),
            ContractKey.ForOffer("npc-jessi", elapsedDays: 13, timeOfDay: 1200));
    }

    /// <summary>
    /// Midnight is a time like any other. A key built at 00:00 must be a real
    /// key, because the alternative is that every offer arriving in that minute
    /// is unclaimable and gets answered repeatedly.
    /// </summary>
    [Fact]
    public void An_offer_that_arrives_at_midnight_is_keyed_like_any_other()
    {
        string midnight = ContractKey.ForOffer("npc-jessi", elapsedDays: 5, timeOfDay: 0);

        Assert.False(string.IsNullOrWhiteSpace(midnight));
        Assert.NotEqual(ContractKey.ForOffer("npc-jessi", elapsedDays: 4, timeOfDay: 0), midnight);
    }

    /// <summary>
    /// Two customers can be offered a contract in the same minute, and on a busy
    /// evening they will be.
    /// </summary>
    [Fact]
    public void Two_customers_offering_in_the_same_minute_are_two_contracts()
    {
        Assert.NotEqual(
            ContractKey.ForOffer("npc-jessi", elapsedDays: 4, timeOfDay: 2330),
            ContractKey.ForOffer("npc-benji", elapsedDays: 4, timeOfDay: 2330));
    }

    /// <summary>
    /// The same offer read twice — on the same frame or five seconds apart — is
    /// one string. Every caller goes through this, which is the point of it
    /// being one function.
    /// </summary>
    [Fact]
    public void One_offer_read_twice_is_one_key()
    {
        Assert.Equal(
            ContractKey.ForOffer("npc-jessi", elapsedDays: 4, timeOfDay: 2330),
            ContractKey.ForOffer("npc-jessi", elapsedDays: 4, timeOfDay: 2330));
    }

    /// <summary>
    /// A customer that despawned mid-read has no id, and the adapter passes what
    /// it has rather than inventing one. An empty key is refused by the registry,
    /// which is the safe end of the trade: nothing is acted on.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_offer_the_game_could_not_identify_has_no_key(string? npcId)
    {
        Assert.Equal(string.Empty, ContractKey.ForOffer(npcId, elapsedDays: 4, timeOfDay: 2330));
    }

    /// <summary>
    /// The key crosses the network in the sense that every machine derives it
    /// from the same replicated values, so it must not pick up anything local —
    /// no culture-dependent number formatting, no machine clock. Checked by
    /// spelling it out: this is the string, exactly.
    /// </summary>
    [Fact]
    public void The_key_is_the_npc_the_day_and_the_time_and_nothing_else()
    {
        Assert.Equal("npc-jessi#4:2330", ContractKey.ForOffer("npc-jessi", 4, 2330));
    }
}
