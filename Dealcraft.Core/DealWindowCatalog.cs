using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Deal scheduling's own settings: the four window switches, each carrying the
/// hours the game reports for its window.
/// </summary>
/// <remarks>
/// <para>
/// These sit alongside <see cref="AutomationCatalog"/>'s settings rather than
/// inside them: the feature switches say what the automation does, these say
/// which windows it may use. Both are <see cref="AutomationSetting"/> and both
/// are keyed on their MelonPreferences entry name, so the file and the app are
/// demonstrably the same setting.
/// </para>
/// <para>
/// Nothing here knows what hours a window has. The adapter reads
/// <c>DealWindowInfo</c> at runtime and passes the readings in, so a player who
/// sees "Afternoon 12:00 to 18:00" is reading the game and not this file.
/// </para>
/// </remarks>
public static class DealWindowCatalog
{
    /// <summary>The MelonPreferences entry name for one window's switch.</summary>
    public static string KeyOf(DealWindow window) => window switch
    {
        DealWindow.Morning => "AllowMorningWindow",
        DealWindow.Afternoon => "AllowAfternoonWindow",
        DealWindow.Night => "AllowNightWindow",
        DealWindow.LateNight => "AllowLateNightWindow",
        _ => $"Allow{window}Window",
    };

    /// <param name="allowed">Which windows the host currently permits.</param>
    /// <param name="hours">
    /// What the game reports for each window. May be short or empty when no
    /// save is loaded, in which case the row says the hours could not be read
    /// rather than inventing them.
    /// </param>
    public static IReadOnlyList<AutomationSetting> Describe(
        DealWindowSet allowed,
        IReadOnlyList<DealWindowHours> hours)
    {
        var rows = new List<AutomationSetting>(DealWindowSet.All.Count);
        foreach (DealWindow window in DealWindowSet.All)
        {
            rows.Add(new AutomationSetting(
                KeyOf(window),
                DealWindowName.Of(window),
                AutomationSetting.OnOff(allowed.Allows(window)),
                summary: Hours(window, hours),
                hostOnly: true,
                change: AutomationChoice.Switch(allowed.Allows(window))));
        }

        return rows;
    }

    /// <summary>
    /// The window's clock times, as the game reports them. Never written down
    /// here: a patch that moves a window must move this line with it.
    /// </summary>
    private static string Hours(DealWindow window, IReadOnlyList<DealWindowHours> hours)
    {
        if (DealWindowHours.Find(hours, window) is DealWindowHours found && found.Available)
        {
            return found.Range;
        }

        return "hours the game has not reported yet";
    }
}
