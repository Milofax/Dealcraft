namespace Dealcraft.Core;

/// <summary>
/// How a window is spelled for a person. The enum spells the last one
/// <c>LateNight</c>; a player reads "Late Night", and every line the mod writes
/// uses the player's spelling.
/// </summary>
public static class DealWindowName
{
    public static string Of(DealWindow window) => window switch
    {
        DealWindow.Morning => "Morning",
        DealWindow.Afternoon => "Afternoon",
        DealWindow.Night => "Night",
        DealWindow.LateNight => "Late Night",
        _ => window.ToString(),
    };
}
