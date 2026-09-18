namespace Dealcraft.Core;

/// <summary>
/// Asks the customer whether they would accept <paramref name="totalPrice"/>
/// for <paramref name="quantity"/> units, and answers accept or refuse.
/// </summary>
/// <remarks>
/// The core never knows where the verdict comes from; in tests it is a
/// synthetic customer.
/// <para>
/// In the game it must be built with <see cref="OfferAcceptance.Probe"/>, over
/// <c>Customer.GetOfferSuccessChance</c>. <c>Customer.EvaluateCounteroffer</c>
/// is the obvious candidate and is the wrong one: it rolls
/// <c>UnityEngine.Random.Range</c> into its answer, so bisecting it finds noise
/// rather than a boundary. See <c>docs/native-truth.md</c>.
/// </para>
/// <para>
/// The verdict is therefore where the confidence is applied: "accepted" means
/// the game's success chance clears it, and the whole curve is drawn at that
/// confidence. The search itself knows nothing about probability.
/// </para>
/// <para>
/// <b>That confidence is a rung of <see cref="ConfidenceSearch.Rungs"/> and not
/// the player's setting.</b> It used to be
/// <see cref="AdvisorSettings.AcceptanceProbabilityThreshold"/>; the search now
/// draws a curve at every rung and keeps whichever pays best, and the player's
/// number is a floor on the winner rather than the confidence it was searched
/// at. See <see cref="ChanceFloor"/>.
/// </para>
/// </remarks>
public delegate bool OfferProbe(int quantity, float totalPrice);
