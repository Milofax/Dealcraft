using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// Decides whether a standing offer may be countered automatically, and
/// whether the counter the curve found is worth sending. Pure: no game types,
/// no clock, no I/O.
/// </summary>
/// <remarks>
/// <para>
/// The decision arrives in two halves for the same reason the scheduler's does:
/// claiming a contract is not a question, it is an act.
/// <see cref="Consider"/> answers everything that can be asked without side
/// effects and ends at <see cref="CounterofferOutcome.Claim"/>; the caller then
/// claims the contract through <see cref="ContractClaimRegistry"/>, builds the
/// curve, and brings both back to <see cref="Send"/>. Nothing is claimed for an
/// offer that was never going to be countered, and nothing is countered twice.
/// </para>
/// <para>
/// There are two locks against acting twice and they guard different things.
/// The claim registry is in memory and covers a session: six machines holding
/// the same replicated contract, and a sweep that comes round again while the
/// first counter is still in flight. The game's own
/// <see cref="PendingOffer.AlreadyCountered"/> is in the save and covers a
/// reload, when the registry is empty again.
/// </para>
/// </remarks>
public static class CounterofferGate
{
    /// <summary>
    /// Everything that can be decided about an offer before touching anything.
    /// </summary>
    /// <param name="offer">The standing offer, read off the customer.</param>
    /// <param name="authority">Whether this machine may act at all.</param>
    /// <param name="automationOn">The host's <c>AutoCounterOffer</c> switch.</param>
    public static CounterofferDecision Consider(
        in PendingOffer offer,
        ServerAuthority authority,
        bool automationOn)
    {
        if (!automationOn)
        {
            return CounterofferDecision.Skip("automatic counter-offers are switched off");
        }

        switch (authority)
        {
            // Both, and not one condition: a guest with a live client half
            // could make this call and must not. One shared conversation, one
            // shared contract list — six installs must not answer them six
            // times. Only the handover is per player; see HandoverGate.
            case ServerAuthority.NotTheServer:
            case ServerAuthority.Guest:
                return CounterofferDecision.Skip(
                    "this machine is not the server, so the host decides this one");
            case ServerAuthority.WithoutAClient:
                return CounterofferDecision.Skip(
                    "the server is not connected as a client yet, and the counter-offer call needs that");
        }

        if (!offer.HasOfferedContract)
        {
            return CounterofferDecision.Skip($"{Who(offer)} has no offer on the table");
        }

        if (offer.AlreadyOnADeal)
        {
            return CounterofferDecision.Skip($"{Who(offer)} is already on a deal");
        }

        if (offer.AlreadyCountered)
        {
            return CounterofferDecision.Skip($"{Who(offer)}'s offer has already been countered once");
        }

        // There is no exclusion list any more, and it is not a regression: the
        // game has a per-customer opt-out of its own. Assigning the customer to
        // a dealer makes the game's own pass route the offer to the dealer
        // instead of notifying the player, so no offer from that customer ever
        // reaches this gate. Ticket 37 read that out; app.md records the caveat,
        // which is that the game's control reads as "give this customer away"
        // rather than "leave them alone".
        if (string.IsNullOrWhiteSpace(offer.ContractKey))
        {
            return CounterofferDecision.Skip(
                $"{Who(offer)}'s offer has no stable key, so it cannot be claimed");
        }

        return CounterofferDecision.Claim($"{Who(offer)}'s offer may be countered");
    }

    /// <summary>
    /// Turn a claim and a planned offer into a counter-offer, or into the
    /// reason there is none.
    /// </summary>
    /// <param name="claim">
    /// What <see cref="ContractClaimRegistry.Claim"/> answered. Only
    /// <see cref="ClaimOutcome.Claimed"/> is permission to act; the others are
    /// reported as they were given, so a contract a player opened by hand says
    /// exactly that rather than blaming the price.
    /// </param>
    /// <param name="planned">
    /// What <see cref="CounterofferPlan.Offer"/> made of the curve — the same
    /// value the Apply button puts on the screen.
    /// </param>
    /// <param name="chance">
    /// The game's own probability that this counter is accepted, 0..1, for the
    /// offer that will actually be made. The search computes it already.
    /// </param>
    /// <param name="offeredPrice">What the customer is offering as it stands.</param>
    /// <param name="floor">
    /// The player's own <see cref="ChanceFloor"/>, 0..1: the chance below which
    /// they would rather keep the offer on the table. Applied here and nowhere
    /// else, because here is after the search has finished choosing.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Countering is a bet, and the stake is the offer already on the table.</b>
    /// <c>Customer.SendCounteroffer</c> calls <c>MSGConversation.ClearResponses</c>,
    /// so the buttons that would have accepted the standing offer are gone —
    /// there is exactly one counteroffer and no way back. And the answer carries
    /// a uniform random term, so no price guarantees a yes. Both readings are in
    /// the project notes.
    /// </para>
    /// <para>
    /// So the comparison is not "is the counter bigger". It is
    /// <c>chance × counter</c> against what is certain. The rule this replaced
    /// asked whether the gain cleared a fraction of the offered price and threw
    /// the chance away — and at the shipped defaults it lost money: a standing
    /// offer of $100 countered at $110 with 0.9 confidence is an expected $99,
    /// against $100 that was already held. That is not a display defect; the
    /// gate was built out of the wrong quantity.
    /// </para>
    /// <para>
    /// <b>One assumption, named.</b> This treats a refused counter as worth
    /// nothing, which is what "one counteroffer, then yes or no" means. If a
    /// refusal in fact leaves some residue the break-even moves slightly in
    /// favour of countering — but not far enough to put the case above back on
    /// the right side of certain money.
    /// </para>
    /// <para>
    /// <b>The floor is the player's, and it comes after the search rather than
    /// into it.</b> <see cref="ConfidenceSearch.Best"/> has already picked the
    /// rung that earns most, and nothing here changes that choice — the floor
    /// only refuses the winner when the winner is a bigger gamble than this
    /// player wanted to take. A number that entered the ranking instead would
    /// re-open the very defect the paragraph above describes.
    /// </para>
    /// <para>
    /// <b>Neither side of that comparison is clamped.</b> The game's own figure
    /// can exceed 1, so a chance of 1.01 satisfies a floor of 1.0;
    /// <see cref="Figures.Fraction"/> and <see cref="Figures.Percent"/> both hold
    /// a figure inside 0..1 and would make the two indistinguishable. The money
    /// below is a different matter and is clamped on purpose.
    /// </para>
    /// </remarks>
    public static CounterofferDecision Send(
        ClaimDecision claim,
        in PlannedOffer planned,
        float chance,
        float offeredPrice,
        float floor)
    {
        if (claim.Outcome != ClaimOutcome.Claimed)
        {
            return CounterofferDecision.Skip(claim.Reason);
        }

        if (!planned.HasOffer)
        {
            return CounterofferDecision.Skip(planned.Reason);
        }

        // The one comparison in the mod that must not be clamped, and it is
        // OfferAcceptance's rather than a second copy of >= written here.
        if (!OfferAcceptance.ClearsThreshold(chance, floor))
        {
            return CounterofferDecision.Skip(
                $"countering at {Money(planned.TotalPrice)} would be taken "
                + $"{ChanceFloor.Spelled(chance)} of the time, under the "
                + $"{ChanceFloor.Spelled(floor)} you set, so the offer on the table stands");
        }

        // Held inside 0..1 before it is multiplied by money: a chance the game
        // reported a shade outside its own range must not inflate the bet.
        float worth = Figures.Fraction(chance) * planned.TotalPrice;

        if (worth <= offeredPrice)
        {
            return CounterofferDecision.Skip(
                $"countering at {Money(planned.TotalPrice)} is worth {Money(worth)} at "
                + $"{Figures.Percent(chance)}, against {Money(offeredPrice)} already on the "
                + "table, and sending it takes that offer away");
        }

        return CounterofferDecision.Send(
            planned,
            $"{planned.Quantity} at {Money(planned.TotalPrice)} is worth {Money(worth)} at "
            + $"{Figures.Percent(chance)} against an offered {Money(offeredPrice)}: {planned.Reason}");
    }

    private static string Who(in PendingOffer offer) =>
        string.IsNullOrWhiteSpace(offer.CustomerName) ? "the customer" : offer.CustomerName;

    private static string Money(float amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
