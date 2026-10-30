using System;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Accepting a deal walks the phone, the journal, the quest manager and the
/// clock. If any of them is missing the game throws halfway, with a contract half
/// built and the offer still standing, so the question is asked before anything
/// is claimed.
/// </summary>
public class TheGameCanTakeTheDealTests
{
    public static TheoryData<string> PassesThatActOnOffers() => new()
    {
        LifecyclePass.Scheduling,
        LifecyclePass.Counteroffers,
    };

    public static TheoryData<string> PassesThatDoNot() => new()
    {
        LifecyclePass.Handover,
        LifecyclePass.Prices,
    };

    [Fact]
    public void Everything_up_reads_as_no_gap()
    {
        Assert.Equal(string.Empty, AcceptPath.Gap(("phone", true), ("journal", true)));
    }

    [Fact]
    public void A_gap_names_every_missing_piece_in_order()
    {
        Assert.Equal(
            "phone, clock",
            AcceptPath.Gap(("phone", false), ("journal", true), ("clock", false)));
    }

    [Theory]
    [MemberData(nameof(PassesThatActOnOffers))]
    public void A_missing_phone_stops_a_pass_that_acts_on_offers(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(Ready(pass) with { AcceptPathGap = "phone" });

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
        Assert.Contains("phone", verdict.Reason, StringComparison.Ordinal);
        Assert.Contains(pass, verdict.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// A reading nobody took is not a reading that came back clean. Left unset,
    /// the gate stands down rather than assuming the phone is there.
    /// </summary>
    [Theory]
    [MemberData(nameof(PassesThatActOnOffers))]
    public void An_unread_accept_path_stops_a_pass_that_acts_on_offers(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(Ready(pass) with { AcceptPathGap = null });

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
    }

    [Theory]
    [MemberData(nameof(PassesThatDoNot))]
    public void The_accept_path_is_no_concern_of_a_pass_that_never_accepts(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(Ready(pass) with { AcceptPathGap = "phone" });

        Assert.Equal(LifecycleOutcome.Act, verdict.Outcome);
    }

    /// <summary>
    /// A reload can leave the game calling itself loaded while it rebuilds the
    /// world, so the loaded flag alone does not say the session is running.
    /// </summary>
    [Theory]
    [MemberData(nameof(PassesThatActOnOffers))]
    [MemberData(nameof(PassesThatDoNot))]
    public void Nothing_acts_while_the_game_is_loading(string pass)
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(Ready(pass) with { Loading = true });

        Assert.Equal(LifecycleOutcome.StandDown, verdict.Outcome);
        Assert.Contains("loading", verdict.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_guest_is_told_it_is_not_the_server_before_it_hears_about_a_phone()
    {
        LifecycleVerdict verdict = LifecycleGate.Consider(Ready(LifecyclePass.Scheduling) with
        {
            Authority = ServerAuthority.Guest,
            AcceptPathGap = "phone",
        });

        Assert.Contains("not the server", verdict.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// The sweep reads the gate first, and the claim comes later in the same
    /// frame, so a gate answering no leaves nothing spent. The order inside the
    /// sweep cannot run in a test, so it is held to the source.
    /// </summary>
    [Fact]
    public void The_sweep_asks_before_it_claims()
    {
        string source = AdapterSource.Read("DealSchedulingLoop.cs");

        int gate = source.IndexOf("Lifecycle(configuration.Enabled)", StringComparison.Ordinal);
        int ready = source.IndexOf("_patience.Consider(", StringComparison.Ordinal);
        int claim = source.IndexOf("_claims.Claim(", StringComparison.Ordinal);

        Assert.True(gate >= 0 && gate < ready && ready < claim);
    }

    private static LifecycleConditions Ready(string pass) => new()
    {
        Pass = pass,
        Enabled = true,
        Authority = ServerAuthority.Held,
        Shared = LifecyclePass.IsShared(pass),
        SaveLoaded = true,
        AcceptPathGap = string.Empty,
    };
}
