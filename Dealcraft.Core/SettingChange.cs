namespace Dealcraft.Core;

/// <summary>
/// One setting the app is asking to have written: the MelonPreferences entry
/// name and the text to put in it.
/// </summary>
/// <remarks>
/// The app never writes anything itself and never keeps the new value. It says
/// what it wants written, the adapter hands that to the feature that owns the
/// entry, the category is flushed, and the app then re-reads the file. That is
/// what keeps the file the one store: the app is a reading of it before the
/// change and a reading of it afterwards, never a copy that has diverged.
/// </remarks>
public sealed class SettingChange
{
    public SettingChange(string key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>The MelonPreferences entry name.</summary>
    public string Key { get; }

    /// <summary>The value to write, spelled as the file spells it.</summary>
    public string Value { get; }
}

/// <summary>
/// What pressing a row did: it moved the selection, or it asked for a write, or
/// neither.
/// </summary>
public readonly struct AppActivation
{
    private AppActivation(bool selectionChanged, SettingChange? change)
    {
        SelectionChanged = selectionChanged;
        Change = change;
    }

    /// <summary>Nothing happened: the row is gone, or there was nothing to do.</summary>
    public static AppActivation Nothing { get; } = new(false, null);

    /// <summary>Whether the detail panel now shows a different row.</summary>
    public bool SelectionChanged { get; }

    /// <summary>What to write, or null when the press only read a row.</summary>
    public SettingChange? Change { get; }

    public static AppActivation Selected() => new(true, null);

    public static AppActivation Write(SettingChange change) => new(false, change);
}
