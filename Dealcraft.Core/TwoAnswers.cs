using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// One question with two answers, exactly one of them ticked. The shape every
/// block of the page that is not the schedule is drawn in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a tick box, and the difference is the whole of it.</b> The off state
/// is a policy — <em>use the game's own behaviour</em> — and a cleared checkbox
/// does not say that. It says nothing at all, which is how a player tells "I
/// have not switched this on" from "this is broken" by guessing. Two answers
/// side by side, one of them always ticked, say which of the two the mod is
/// doing.
/// </para>
/// <para>
/// Either answer writes the whole question, including the one already ticked. A
/// radio whose selected option does nothing when pressed is a radio that can be
/// left showing something the preferences file does not say.
/// </para>
/// <para>
/// <see cref="PriceMaintenanceCatalog"/> is where this shape started, for the
/// listed price alone, and it is generalised here because the owner asked for
/// every block to read the same way.
/// </para>
/// </remarks>
public static class TwoAnswers
{
    /// <summary>
    /// What the off answer is called wherever off means <em>the game does this
    /// the way it always did</em>. Named rather than repeated, because three
    /// blocks say it and three spellings of one state would read as three
    /// states.
    /// </summary>
    public const string TheGamesOwnBehaviour = "Manual (game's default)";

    /// <summary>The row that answers "no", for a setting's entry name.</summary>
    public static string No(string key) => key + ":manual";

    /// <summary>The row that answers "yes", for a setting's entry name.</summary>
    public static string Yes(string key) => key + ":automated";

    /// <param name="setting">
    /// The entry the question writes. Its title is the "yes" answer, because
    /// that is the one that has to say what it automates — the two price blocks
    /// cannot be told apart by their headings alone, and both of us confused
    /// them.
    /// </param>
    /// <param name="no">What the off state is called. Usually the game's own behaviour.</param>
    /// <param name="indent">How far in from the block's edge both answers sit.</param>
    public static IReadOnlyList<FormRow> Rows(AutomationSetting setting, string no, int indent = 1)
    {
        bool yes = setting.SwitchedOn;

        return new[]
        {
            new FormRow(
                No(setting.Key),
                FormRowKind.Choice,
                no,
                chosen: !yes,
                indent: indent,
                press: new[] { new SettingChange(setting.Key, AutomationSetting.OnOff(false)) }),
            new FormRow(
                Yes(setting.Key),
                FormRowKind.Choice,
                setting.Title,

                // The one footnote a question is allowed, and only where its two
                // answers cannot say it themselves.
                hint: setting.Summary,
                chosen: yes,
                indent: indent,
                press: new[] { new SettingChange(setting.Key, AutomationSetting.OnOff(true)) }),
        };
    }
}
