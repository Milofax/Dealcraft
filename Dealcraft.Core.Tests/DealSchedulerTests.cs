using System;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class DealSchedulerTests
{
    /// <summary>
    /// An offer with nothing wrong with it: every test below spoils exactly one
    /// thing, so the reason it is skipped is never in doubt.
    /// </summary>
    private static PendingOffer Sound(
        string contractKey = "mrs-ming#3:1400",
        string customerName = "Mrs. Ming",
        bool hasOfferedContract = true,
        bool alreadyOnADeal = false) =>
        new(contractKey, customerName, hasOfferedContract, alreadyOnADeal);

    private static AdvisorSettings NoExclusions() => new();

    [Fact]
    public void A_sound_offer_on_the_server_is_worth_claiming()
    {
        ScheduleDecision decision = DealScheduler.Consider(
            Sound(), ServerAuthority.Held, automationOn: true);

        Assert.Equal(ScheduleOutcome.Claim, decision.Outcome);
    }

    /// <summary>
    /// The default. A fresh install must not change a running save, so nothing
    /// happens until the host turns scheduling on.
    /// </summary>
    [Fact]
    public void Nothing_is_scheduled_while_the_feature_is_off()
    {
        ScheduleDecision decision = DealScheduler.Consider(
            Sound(), ServerAuthority.Held, automationOn: false);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("switched off", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Six players may each have the mod installed and each see the same
    /// customer state. Without this, six machines act on one contract.
    /// </summary>
    [Fact]
    public void A_machine_that_is_not_the_server_does_nothing()
    {
        ScheduleDecision decision = DealScheduler.Consider(
            Sound(), ServerAuthority.NotTheServer, automationOn: true);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("not the server", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The accept RPC is written by the client half of the host and refuses to
    /// send without it, so being the server is necessary and not sufficient.
    /// </summary>
    [Fact]
    public void A_server_whose_client_half_is_not_up_yet_waits()
    {
        ScheduleDecision decision = DealScheduler.Consider(
            Sound(), ServerAuthority.WithoutAClient, automationOn: true);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("not connected", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_customer_with_no_offer_on_the_table_is_left_alone()
    {
        PendingOffer offer = Sound(hasOfferedContract: false);

        ScheduleDecision decision = DealScheduler.Consider(
            offer, ServerAuthority.Held, automationOn: true);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("no offer", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_customer_already_on_a_deal_is_left_alone()
    {
        PendingOffer offer = Sound(alreadyOnADeal: true);

        ScheduleDecision decision = DealScheduler.Consider(
            offer, ServerAuthority.Held, automationOn: true);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("already on a deal", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Without a key the claim registry cannot tell two contracts apart, and
    /// acting once per contract stops being provable.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_offer_with_no_stable_key_is_left_alone(string contractKey)
    {
        PendingOffer offer = Sound(contractKey: contractKey);

        ScheduleDecision decision = DealScheduler.Consider(
            offer, ServerAuthority.Held, automationOn: true);

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Contains("stable key", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The feature switch is checked before anything else, so the log of a host
    /// who has not turned scheduling on stays empty rather than filling with
    /// reasons about customers they never asked about.
    /// </summary>
    [Fact]
    public void The_feature_switch_is_answered_before_anything_else_is_examined()
    {
        PendingOffer nothingRight = new(
            contractKey: "",
            customerName: "",
            hasOfferedContract: false,
            alreadyOnADeal: true);

        ScheduleDecision decision = DealScheduler.Consider(
            nothingRight, ServerAuthority.NotTheServer, automationOn: false);

        Assert.Contains("switched off", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The claim registry is the thing that makes it exactly once; the
    /// scheduler only acts on the one outcome that is permission to act.
    /// </summary>
    [Fact]
    public void Only_a_granted_claim_is_permission_to_choose_a_window()
    {
        var rotation = new DealWindowRotation();
        var windows = new DealWindowSet();
        windows.Allow(DealWindow.Night, true);

        ScheduleDecision decision = DealScheduler.ChooseWindow(
            ClaimDecision.Claimed("mine"), windows, DealWindowSet.All, DealWindow.Morning, rotation);

        Assert.Equal(ScheduleOutcome.Schedule, decision.Outcome);
        Assert.Equal(DealWindow.Night, decision.Window);
    }

    [Theory]
    [InlineData(ClaimOutcome.AlreadyClaimed)]
    [InlineData(ClaimOutcome.LockedByHuman)]
    [InlineData(ClaimOutcome.NotEligible)]
    public void A_claim_that_was_not_granted_schedules_nothing(ClaimOutcome outcome)
    {
        var windows = new DealWindowSet();
        windows.Allow(DealWindow.Night, true);

        ClaimDecision claim = outcome switch
        {
            ClaimOutcome.AlreadyClaimed => ClaimDecision.AlreadyClaimed("already claimed"),
            ClaimOutcome.LockedByHuman => ClaimDecision.LockedByHuman("a player opened it"),
            _ => ClaimDecision.NotEligible("no key"),
        };

        ScheduleDecision decision = DealScheduler.ChooseWindow(
            claim, windows, DealWindowSet.All, DealWindow.Morning, new DealWindowRotation());

        Assert.Equal(ScheduleOutcome.Skip, decision.Outcome);
        Assert.Equal(claim.Reason, decision.Reason);
    }

    /// <summary>
    /// A contract a human has touched is locked out of automation for good, and
    /// the log must say that rather than blaming the window set.
    /// </summary>
    [Fact]
    public void A_contract_a_player_touched_says_so_rather_than_blaming_the_windows()
    {
        ScheduleDecision decision = DealScheduler.ChooseWindow(
            ClaimDecision.LockedByHuman("a player opened 'ming#3' by hand"),
            new DealWindowSet(),
            DealWindowSet.All,
            DealWindow.Morning,
            new DealWindowRotation());

        Assert.Contains("by hand", decision.Reason);
    }
}
