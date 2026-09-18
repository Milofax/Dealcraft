using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The one safety rule, for every pass that acts on the world: whether anything
/// may be done at all right now.
///
/// It used to be stated for one pass alone, which meant the three other
/// loops — scheduling, counter-offers, handover — had no save guard at all and
/// would happily send a vanilla RPC into the middle of
/// <c>SaveManager.IsSaving</c>. That is the defect this type exists to close, so
/// the cases below are written once and every pass is named in them.
/// </summary>
public class LifecycleGateTests
{
    /// <summary>Every pass that acts, named as its log names it.</summary>
    public static TheoryData<string> EveryPass() => new()
    {
        LifecyclePass.Scheduling,
        LifecyclePass.Counteroffers,
        LifecyclePass.Handover,
        LifecyclePass.Prices,
    };

    /// <summary>Every pass that acts on state the whole session shares.</summary>
    public static TheoryData<string> EverySharedPass() => new()
    {
        LifecyclePass.Scheduling,
        LifecyclePass.Counteroffers,
        LifecyclePass.Prices,
    };

    [Theory]
    [MemberData(nameof(EveryPass))]
    public void A_host_who_never_turned_a_pass_on_gets_nothing(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(Everything(pass) with { Enabled = false });

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
        Assert.Contains("switched off", verdict.Reason);
    }

    /// <summary>
    /// The lesson the original paid for with save-state bugs, and the one
    /// <c>docs/native-truth.md</c> records: never act while the game is writing
    /// the save. Waiting costs a frame; acting into a snapshot of the world
    /// costs the save file. Every pass obeys it, not just the one that was
    /// written last.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPass))]
    public void No_pass_acts_while_the_game_is_saving(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(Everything(pass) with { Saving = true });

        Assert.Equal(LifecycleOutcome.Wait, verdict.Outcome);
        Assert.Contains("saving", verdict.Reason);
    }

    /// <summary>
    /// The main menu, and the seconds of a load before the world exists. Story
    /// 19 asks the mod to do nothing while no save is loaded, and the seconds
    /// after "Load" is pressed are exactly when a half-built world is easiest to
    /// act on.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPass))]
    public void No_pass_acts_while_no_save_is_loaded(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(Everything(pass) with { SaveLoaded = false });

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
        Assert.Contains("no save", verdict.Reason);
    }

    /// <summary>
    /// A machine with no network at all acts for nobody, whatever the pass is:
    /// the vanilla calls are all written through the client half.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPass))]
    public void A_machine_with_no_connection_never_acts(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(
            Everything(pass) with { Authority = ServerAuthority.NotTheServer });

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
    }

    /// <summary>
    /// The multiplayer rule, and it is two rules. A pass acting on state the
    /// whole session shares — one customer roster, one contract list, one
    /// conversation, one price table — runs on the host alone, because six
    /// installs must not answer one offer six times.
    /// </summary>
    [Theory]
    [MemberData(nameof(EverySharedPass))]
    public void A_guest_never_acts_on_state_the_session_shares(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(
            Everything(pass) with { Authority = ServerAuthority.Guest });

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
        Assert.Contains("not the server", verdict.Reason);
    }

    /// <summary>
    /// The other half, and the point of the asymmetry. A handover is one
    /// player's goods leaving one player's pockets, so it runs wherever that
    /// player is — <c>Customer.ProcessHandover</c> is a client path and
    /// <c>Customer.IsReadyForHandover</c> reads the local player singleton, so
    /// on a guest both answer about the guest.
    /// </summary>
    [Fact]
    public void A_guest_hands_over_their_own_goods()
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(
            Everything(LifecyclePass.Handover) with { Authority = ServerAuthority.Guest });

        Assert.Equal(LifecycleOutcome.Act, verdict.Outcome);
    }

    /// <summary>
    /// Said where it can be read back: exactly one pass is per player. A new
    /// pass that forgets to declare itself shared fails here rather than
    /// quietly acting six times.
    /// </summary>
    [Fact]
    public void Only_the_handover_is_per_player()
    {
        Assert.False(LifecyclePass.IsShared(LifecyclePass.Handover));

        foreach (string pass in new[]
                 {
                     LifecyclePass.Scheduling,
                     LifecyclePass.Counteroffers,
                     LifecyclePass.Prices,
                 })
        {
            Assert.True(LifecyclePass.IsShared(pass), pass);
        }
    }

    /// <summary>
    /// The server's own client half is not up yet, which the vanilla calls need.
    /// Transient, so the pass waits rather than standing down: a moment later
    /// the same work is doable.
    /// </summary>
    [Fact]
    public void A_server_without_a_client_waits_rather_than_standing_down()
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(
            Everything(LifecyclePass.Scheduling) with { Authority = ServerAuthority.WithoutAClient });

        Assert.Equal(LifecycleOutcome.Wait, verdict.Outcome);
    }

    /// <summary>
    /// A guest disconnecting is not the host losing authority. The host still
    /// holds the server and its own client half, so the automation carries on
    /// for the players who are still there — which is the ticket's "automation
    /// continues on the host" stated at the seam. The gate has no notion of how
    /// many observers there are, and that is the point: nothing about a client
    /// coming or going reaches it.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPass))]
    public void A_client_leaving_or_returning_does_not_stop_the_host(string pass)
    {
        Assert.Equal(LifecycleOutcome.Act, LifecycleGate.Consider(Everything(pass)).Outcome);
    }

    [Theory]
    [MemberData(nameof(EveryPass))]
    public void With_everything_in_place_the_pass_runs(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(Everything(pass));

        Assert.Equal(LifecycleOutcome.Act, verdict.Outcome);
    }

    /// <summary>
    /// A switched-off pass says so before anything else, so a host who never
    /// asked for a feature never reads a word about servers or saving on its
    /// account.
    /// </summary>
    [Fact]
    public void Being_switched_off_is_reported_ahead_of_every_other_reason()
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(new LifecycleConditions
        {
            Pass = LifecyclePass.Handover,
            Enabled = false,
            Authority = ServerAuthority.NotTheServer,
            SaveLoaded = false,
            Saving = true,
            });

        Assert.Contains("switched off", verdict.Reason);
    }

    /// <summary>
    /// Four passes share this gate and every one of them writes its verdict into
    /// the same log. A reason that did not name the pass would leave the host
    /// reading "switched off" four times with no way to tell which switch.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPass))]
    public void Every_reason_names_the_pass_it_is_about(string pass)
    {
        LifecycleConditions conditions = Everything(pass);

        LifecycleVerdict[] verdicts =
        {
            LifecycleGate.Consider(conditions with { Enabled = false }),
            LifecycleGate.Consider(conditions with { SaveLoaded = false }),
            LifecycleGate.Consider(conditions with { Authority = ServerAuthority.NotTheServer }),
            LifecycleGate.Consider(conditions with { Authority = ServerAuthority.WithoutAClient }),
            LifecycleGate.Consider(conditions with { Saving = true }),
            LifecycleGate.Consider(conditions),
        };

        foreach (LifecycleVerdict verdict in verdicts)
        {
            Assert.Contains(pass, verdict.Reason);
        }
    }

    /// <summary>
    /// Four passes, all off on a fresh install, and the mod already announced
    /// every switch when it loaded. Saying it again once per scene per pass is
    /// four lines that tell the host nothing they did not type themselves, so
    /// this one verdict is not worth a line.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPass))]
    public void A_switch_the_host_set_themselves_is_not_worth_repeating(string pass)
    {
        Assert.False(
            LifecycleGate.Consider(Everything(pass) with { Enabled = false }).WorthReporting);
    }

    /// <summary>
    /// Every other reason is news. A host who asked for a pass and is not
    /// getting it is entitled to know why, once.
    /// </summary>
    [Fact]
    public void Every_reason_but_the_switch_is_worth_a_line()
    {
        LifecycleConditions conditions = Everything(LifecyclePass.Scheduling);

        Assert.True(LifecycleGate.Consider(conditions with { SaveLoaded = false }).WorthReporting);
        Assert.True(LifecycleGate.Consider(
            conditions with { Authority = ServerAuthority.NotTheServer }).WorthReporting);
        Assert.True(LifecycleGate.Consider(
            conditions with { Authority = ServerAuthority.WithoutAClient }).WorthReporting);
        Assert.True(LifecycleGate.Consider(conditions with { Saving = true }).WorthReporting);
    }

    /// <summary>
    /// A verdict nobody asked for — an unassigned field, a default struct — must
    /// not read as permission to act. The zero value stands everything down.
    /// </summary>
    [Fact]
    public void A_verdict_that_was_never_reached_permits_nothing()
    {
        LifecycleVerdict verdict = default;

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Reason));
    }

    /// <summary>
    /// A caller that forgot to fill the conditions in must not be granted
    /// anything either. Enabled is false by default, so this falls at the first
    /// hurdle, and the reason still reads as a sentence rather than as a blank.
    /// </summary>
    [Fact]
    public void Conditions_nobody_filled_in_permit_nothing()
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(default);

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Reason));
    }

    /// <summary>
    /// Every verdict goes into the log, so every verdict has to say something a
    /// host can act on.
    /// </summary>
    [Fact]
    public void Every_verdict_carries_a_reason_fit_for_a_log_line()
    {
        LifecycleConditions conditions = Everything(LifecyclePass.Counteroffers);

        LifecycleVerdict[] verdicts =
        {
            LifecycleGate.Consider(conditions with { Enabled = false }),
            LifecycleGate.Consider(conditions with { SaveLoaded = false }),
            LifecycleGate.Consider(conditions with { Authority = ServerAuthority.NotTheServer }),
            LifecycleGate.Consider(conditions with { Authority = ServerAuthority.WithoutAClient }),
            LifecycleGate.Consider(conditions with { Saving = true }),
            LifecycleGate.Consider(conditions),
        };

        foreach (LifecycleVerdict verdict in verdicts)
        {
            Assert.False(string.IsNullOrWhiteSpace(verdict.Reason), $"{verdict.Outcome} has no reason");
        }
    }

    private static LifecycleConditions Everything(string pass) => new()
    {
        Pass = pass,
        Enabled = true,
        Shared = LifecyclePass.IsShared(pass),
        Authority = ServerAuthority.Held,
        SaveLoaded = true,
        Saving = false,
    };
}
