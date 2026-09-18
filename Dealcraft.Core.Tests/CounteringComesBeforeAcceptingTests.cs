using System;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The order the passes are ticked in, held to where it can be held: the
/// registration list in the adapter.
/// </summary>
/// <remarks>
/// <para>
/// This is a source guard rather than a behaviour test because the thing being
/// pinned is a line in an Il2Cpp adapter that no test can run. What it pins is
/// not a style preference. On 2026-09-19 the owner had counter-offers and deal
/// scheduling both switched on, and every deal went through at exactly the
/// price the customer had named. <c>decisions.jsonl</c> recorded no negotiation
/// at all — not a refusal, not a skip, nothing — because scheduling ticked
/// first, accepted the standing offer into an allowed window, and left the
/// counteroffer pass looking at a customer with nothing on the table. That pass
/// treats such a customer as not news and says nothing, so the feature that had
/// never worked also never complained.
/// </para>
/// <para>
/// Countering first is also the order a person would use: ask for a better
/// price, then take the deal. The two passes keep separate claim registries by
/// design, so nothing else makes them take turns.
/// </para>
/// </remarks>
public class CounteringComesBeforeAcceptingTests
{
    [Fact]
    public void The_counteroffer_pass_is_ticked_before_the_scheduling_pass()
    {
        string source = AdapterSource.Read("Features/Features.cs");

        int countering = Registration(source, "CounterofferAutomationFeature");
        int accepting = Registration(source, "DealSchedulingFeature");

        Assert.True(
            countering < accepting,
            "Features.All() must list CounterofferAutomationFeature before DealSchedulingFeature. "
            + "Ticked the other way round, scheduling accepts the customer's own price before the "
            + "counteroffer pass ever sees the offer, and negotiating silently never happens.");
    }

    /// <summary>
    /// Where a feature is constructed in the list, and a failure that names the
    /// feature rather than reporting -1 from somewhere further down.
    /// </summary>
    private static int Registration(string source, string feature)
    {
        int at = source.IndexOf("new " + feature + "()", StringComparison.Ordinal);

        Assert.True(at >= 0, $"Features.All() no longer constructs {feature}.");

        return at;
    }
}
