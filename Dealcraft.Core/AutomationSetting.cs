namespace Dealcraft.Core;

/// <summary>
/// One setting the host can change: what the preferences file calls it, what it
/// currently says, and the one short line the app may put beside the control.
/// </summary>
/// <remarks>
/// <para>
/// Every MelonPreferences entry gets one of these and nothing else does. The
/// spec's rule is that a setting lives in the file and in the app or it does not
/// exist, so this is not a summary of the interesting settings — it is the whole
/// list, and the catalogues are the one place that says what the list is. A
/// switch is not a separate kind of thing here: it is a setting whose
/// <see cref="Value"/> reads On or Off.
/// </para>
/// <para>
/// There used to be two paragraphs on every row — what it does, and what it will
/// never do — printed beside the control in the app. The owner's verdict on that
/// was that it "may as well be Klingon", and he was right: the paragraphs were
/// there because the control did not explain itself. The control now does, so
/// what is left is <see cref="Summary"/>, one short line, held to its length by
/// a test. Where a control needs a paragraph the control is wrong, and the
/// answer is to fix the control rather than to write a better paragraph.
/// </para>
/// </remarks>
public sealed class AutomationSetting
{
    /// <summary>
    /// The longest a summary may be. About one line beside a control on the
    /// phone's screen; anything longer is a paragraph wearing a disguise.
    /// </summary>
    public const int SummaryLimit = 72;

    public AutomationSetting(
        string key,
        string title,
        string value,
        string summary,
        bool hostOnly,
        AutomationChoice change)
    {
        Key = key;
        Title = title;
        Value = value;
        Summary = summary;
        HostOnly = hostOnly;
        Change = change;
    }

    /// <summary>The MelonPreferences entry name. Also the row's identity.</summary>
    public string Key { get; }

    public string Title { get; }

    /// <summary>
    /// What the preferences file currently says, spelled for a reader. The file
    /// is the only store, so this is a reading of it and never a second copy.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// One short line beside the control, or empty where the control says
    /// everything. Never a sentence a player has to work through before they can
    /// press anything.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Whether it only has an effect on the host. The handover and its grade
    /// answer are the two that are not: a handover is one player's goods leaving
    /// one player's pockets, so a guest's own machine does it and a guest's own
    /// copy of those settings decides how.
    ///
    /// It used to say the opposite, naming the settings that shaped what the
    /// advisor overlay showed. The overlay was deleted with ticket 27 and every
    /// setting it read went with it.
    /// </summary>
    public bool HostOnly { get; }

    /// <summary>
    /// What pressing this setting's control does to it: the value it would take
    /// next, or <see cref="AutomationChoice.FileOnly"/> for the ones the app has
    /// nowhere to type into. Required rather than defaulted, so a setting cannot
    /// be added without somebody deciding how a player changes it.
    /// </summary>
    public AutomationChoice Change { get; }

    /// <summary>Whether this setting reads as a switch rather than as a figure.</summary>
    public bool IsSwitch => Value == On || Value == Off;

    /// <summary>Whether a switch is currently on. False for anything else.</summary>
    public bool SwitchedOn => Value == On;

    /// <summary>
    /// How a switch's <see cref="Value"/> is spelled. Here rather than in each
    /// catalogue because one app shows every catalogue's settings, so two
    /// spellings of the same state would read as two different states.
    /// </summary>
    public static string OnOff(bool value) => value ? On : Off;

    private const string On = "On";

    private const string Off = "Off";
}
