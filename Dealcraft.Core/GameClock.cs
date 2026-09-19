using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// Spells one of the game's HHMM integers as a clock time.
/// </summary>
/// <remarks>
/// The game keeps times as integers like 1800 and 2400, so the end of the day
/// stays 24:00 rather than wrapping round to 00:00 of the next one — which is
/// how the game itself shows it. This is a spelling, never a calculation: no
/// hour in the mod is decided here.
/// </remarks>
public static class GameClock
{
    /// <summary>
    /// The game's own spelling when the adapter could get one, and the 24-hour
    /// clock otherwise, so a reading never comes out blank.
    /// </summary>
    public static string Spell(string? gamesOwnLabel, int time) =>
        string.IsNullOrWhiteSpace(gamesOwnLabel) ? Spell(time) : gamesOwnLabel!.Trim();

    public static string Spell(int time)
    {
        int hours = time / 100;
        int minutes = time % 100;
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", hours, minutes);
    }
}
