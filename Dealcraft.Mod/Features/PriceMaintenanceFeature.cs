using System.Collections.Generic;
using Dealcraft.Core;
using MelonLoader;

namespace Dealcraft;

/// <summary>
/// The <c>LISTED PRICE</c> question, and the pass that answers it. Host only, and
/// off on a fresh install like every other automation switch.
/// </summary>
/// <remarks>
/// <para>
/// The owner asked for this in his own words: <i>"Ich möchte einstellen können
/// in den Einstellungen, ob du das auch in Products explizit überall anpasst,
/// die Preise […] Da soll kein Schalter im Products App selber sein. Also ich
/// will nicht, dass du vorhandene Apps von Schedule One umschreibst."</i>
/// </para>
/// <para>
/// So: one entry, on Dealcraft's own settings page, covering every listed
/// product, and nothing whatsoever added to Schedule I's Products app. The price
/// lives in <c>ProductManager</c>, so no game panel has to be touched for the
/// number to change — see <see cref="ListedPriceWriter"/>, which carries the
/// evidence for the one call this makes.
/// </para>
/// </remarks>
internal sealed class PriceMaintenanceFeature : Feature
{
    private FeatureContext _context;
    private MelonPreferences_Entry<bool> _maintaining;
    private ListedPricePass _pass;

    public override string Name => "prices";

    public override void Declare(MelonPreferences_Category category)
    {
        _maintaining = category.CreateEntry(
            PriceMaintenanceCatalog.Key, false,
            description: "Host only: keep every listed product's price at the figure your customers "
                + "still clear, rewriting it as the roster changes. Off by default, and off means "
                + "Dealcraft writes nothing — it does not put back what it wrote earlier, because "
                + "restoring is a different thing from stopping. The write is the game's own "
                + "ProductManager.SetPrice observers RPC, so unmodified clients see it; nothing is "
                + "added to Schedule I's Products app.");
    }

    public override IReadOnlyList<AutomationSetting> Describe() =>
        PriceMaintenanceCatalog.Describe(_maintaining.Value);

    public override bool Change(SettingChange change) => Preference.Set(change, _maintaining);

    public override void Start(FeatureContext context)
    {
        _context = context;
        _pass = new ListedPricePass(
            Configuration,
            context.Lifecycle(),
            context.Log(Name),
            context.Warn(Name));
    }

    public override void SceneChanged() => _pass?.SceneChanged();

    public override void Tick() => _pass?.Tick();

    public override string Summary() => _maintaining.Value
        ? "Listed prices: maintained across every product."
        : null;

    /// <summary>
    /// Everything a pass needs, read fresh each pass so a hand-edited
    /// preferences file takes effect without a restart.
    /// </summary>
    private PriceMaintenanceConfiguration Configuration() =>
        new(_maintaining.Value, _context.Settings());
}
