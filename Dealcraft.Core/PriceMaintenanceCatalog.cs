using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// The one setting the price-maintenance feature owns, and the two rows the
/// <c>LISTED PRICE</c> block draws it as.
/// </summary>
/// <remarks>
/// <para>
/// The owner asked for this by name: <i>"ich möchte einstellen können in den
/// Einstellungen, ob du das auch in Products explizit überall anpasst, die
/// Preise […] Da soll kein Schalter im Products App selber sein."</i> So it is
/// one decision on Dealcraft's own settings page covering every listed product,
/// and Schedule I's Products app is not touched: the price lives in
/// <c>ProductManager</c>, which both of the game's own controls reach through
/// their RPCs, so nothing in the game's UI has to change for the number to.
/// </para>
/// <para>
/// Drawn as a question with two answers rather than as a tick box, because the
/// off state is a policy too — the game's own behaviour is what a player who has
/// priced their book by hand has chosen, not the absence of a choice.
/// </para>
/// </remarks>
public static class PriceMaintenanceCatalog
{
    /// <summary>The preferences entry. One key, one decision, every product.</summary>
    public const string Key = "MaintainListedPrices";

    /// <summary>The row that answers "no".</summary>
    public const string Manual = Key + ":manual";

    /// <summary>The row that answers "yes".</summary>
    public const string Automated = Key + ":automated";

    public static IReadOnlyList<AutomationSetting> Describe(bool maintaining) =>
        new[]
        {
            new AutomationSetting(
                Key,
                "Automated setting in the Price App",
                AutomationSetting.OnOff(maintaining),

                // No line under it. The two price blocks are told apart by their
                // "Automated" answers naming what each automates — this one the
                // price in the game's own Price app, the other the answer to a
                // customer's request — and the drawing carries no footnote here.
                summary: string.Empty,
                hostOnly: true,
                change: AutomationChoice.Switch(maintaining)),
        };

    /// <summary>
    /// The block, as two answers to one question. Exactly one is ticked, and
    /// pressing either writes the entry — pressing the one already ticked writes
    /// what it already says, which is how a radio behaves and how the file stays
    /// the only store.
    /// </summary>
    /// <remarks>
    /// This was the only block drawn this way and is now one of three:
    /// <see cref="TwoAnswers"/> is the same shape, generalised, and this stays
    /// as the listed price's own name for it.
    /// </remarks>
    public static IReadOnlyList<FormRow> Rows(AutomationSetting setting) =>
        TwoAnswers.Rows(setting, TwoAnswers.TheGamesOwnBehaviour);
}
