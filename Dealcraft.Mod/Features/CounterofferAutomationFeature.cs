using Dealcraft.Core;

namespace Dealcraft;

/// <summary>
/// Sends counter-offers at the best price the customer will still take. Host
/// only, and off on a fresh install like every other automation switch.
/// </summary>
/// <remarks>
/// <para>
/// This feature owns no MelonPreferences entry of its own. <c>AutoCounterOffer</c>
/// is one of the shared settings, and <see cref="ModSettings"/> owns it.
/// </para>
/// <para>
/// The switch that turns the automation on at all is <c>AutoCounterOffer</c>,
/// which is shared with the other two automation switches. With it off this
/// feature does nothing at all and the player answers their customers by hand,
/// which is the game's own behaviour and what the page's off state says. It used
/// to name the overlay's Apply button here; the overlay is deleted.
/// </para>
/// </remarks>
internal sealed class CounterofferAutomationFeature : Feature
{
    private FeatureContext _context;
    private CounterofferLoop _counters;

    public override string Name => "counter";

    public override void Start(FeatureContext context)
    {
        _context = context;

        // Its own claim registry, keyed the same way the scheduler's is but not
        // shared with it. Countering an offer and accepting it into a window are
        // two acts on one contract; one registry would let the first stand in
        // for the second and the offer would never get scheduled.
        _counters = new CounterofferLoop(
            new ContractClaimRegistry(),
            context.Lifecycle(),
            Configuration,
            context.Log(Name),
            context.Warn(Name));
    }

    public override void SceneChanged() => _counters?.SceneChanged();

    public override void Tick() => _counters?.Tick();

    /// <summary>
    /// Everything a sweep needs, read fresh each sweep so that a hand-edited
    /// preferences file takes effect without a restart.
    /// </summary>
    private CounterofferConfiguration Configuration()
    {
        AdvisorSettings settings = _context.Settings();
        return new CounterofferConfiguration(settings.AutoCounterOffer, settings);
    }
}
